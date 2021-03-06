import json
from io import BytesIO
from zipfile import ZipFile

import pandas as pd
import requests
from bs4 import BeautifulSoup
from sqlalchemy import create_engine
from datetime import date
import psycopg2

CHUNK_SIZE = 50000
CATCH_DATA_URL = "https://www.fiskeridir.no/Tall-og-analyse/AApne-data/AApne-datasett/Fangstdata-seddel-koblet-med-fartoeydata"
CONFIG_FILE_NAME = "config.json"
YEARS_TO_DOWNLOAD = 3


def clean_column(column):
    column = column.lower()
    column = column.replace(" (", "_")
    column = column.replace(" - ", "")
    column = column.replace(" ", "_")
    column = column.replace("(", "_")
    column = column.replace(")", "")
    return column


class CatchDataDownloader(object):
    ARCHIVE_SUFFIX = ".zip"

    def __init__(self, url):
        self.url = url
        with open(CONFIG_FILE_NAME) as config:
            self.configuration_file = json.load(config)

    def fetch_files_from_remote_endpoint(self):
        r = requests.get(self.url, {})
        html_page = r.content
        soup = BeautifulSoup(html_page, "lxml")
        current_year = date.today().year

        for link in soup.findAll("a"):
            link = link.get("href")
            if not link:
                continue
            if link.endswith(self.ARCHIVE_SUFFIX):
                link_year = int(link[50:54])

                if link_year > current_year - YEARS_TO_DOWNLOAD:
                    self.download_archive_file(link)

    def download_archive_file(self, uri):
        print(uri)
        r = requests.get(uri)
        requested_file = BytesIO(r.content)
        self.read_archived_content(requested_file)

    def read_archived_content(self, file_data):
        # retval = {}
        with ZipFile(file_data, "r") as zipped_contents:
            zipped_files = zipped_contents.namelist()
            for file in zipped_files:
                df = pd.read_csv(zipped_contents.open(file), decimal=",", sep=";")
                locations = []
                for index, row in df.iterrows():
                    try:
                        lok = str(int(row["Hovedområde (kode)"])).zfill(2) + str(int(row["Lokasjon (kode)"])).zfill(2)
                    except (ValueError, KeyError):
                        continue
                    locations.append(lok)

                df["lok"] = pd.Series(locations)
                cols = df.columns
                df.columns = [clean_column(col) for col in df.columns]
                print("Writing file: {file} to database".format(file=file))
                try:
                    username = self.configuration_file["postgresSettings"]["username"]
                    password = self.configuration_file["postgresSettings"]["password"]
                    host = self.configuration_file["postgresSettings"]["host"]
                    port = self.configuration_file["postgresSettings"]["port"]
                    database_name = self.configuration_file["postgresSettings"]["databaseName"]
                    table_name = self.configuration_file["postgresSettings"]["catchDataStagingTableName"]
                    truncate_raw_catch_data_staging_table_stored_procedure_name = self.configuration_file["postgresSettings"][
                        "truncateRawCatchDataStagingTableStoredProcedureName"]
                    connection_string = str(self.configuration_file["postgresSettings"]["connectionString"]).format(username=username, password=password,
                                                                                                                    host=host, port=port,
                                                                                                                    databaseName=database_name)

                    engine = create_engine(connection_string)
                    df.to_sql(table_name, engine, if_exists="append", chunksize=CHUNK_SIZE)

                    connection = psycopg2.connect(dbname=database_name, user=username, password=password, host=host, port=port)
                    cursor = connection.cursor()
                    cursor.execute("CALL {stored_procedure}();".format(stored_procedure=truncate_raw_catch_data_staging_table_stored_procedure_name))
                    connection.commit()
                except Exception as e:
                    print("Failed to add data from file: {file}".format(file="Nordic model"))
                    raise e


if __name__ == '__main__':
    downloader = CatchDataDownloader(CATCH_DATA_URL)
    downloader.fetch_files_from_remote_endpoint()
