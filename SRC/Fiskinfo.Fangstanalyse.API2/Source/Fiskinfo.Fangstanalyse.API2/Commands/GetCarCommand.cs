using System;
using System.Threading;
using System.Threading.Tasks;
using Fiskinfo.Fangstanalyse.API2.Models;
using Fiskinfo.Fangstanalyse.API2.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Net.Http.Headers;
using SIntefSecureFramework.Mapping;

namespace Fiskinfo.Fangstanalyse.API2.Commands
{
    public class GetCarCommand : IGetCarCommand
    {
        private readonly IActionContextAccessor actionContextAccessor;
        private readonly ICarRepository carRepository;
        private readonly IMapper<Car, ViewModels.Car> carMapper;

        public GetCarCommand(
            IActionContextAccessor actionContextAccessor,
            ICarRepository carRepository,
            IMapper<Car, ViewModels.Car> carMapper)
        {
            this.actionContextAccessor = actionContextAccessor;
            this.carRepository = carRepository;
            this.carMapper = carMapper;
        }

        public async Task<IActionResult> ExecuteAsync(int carId, CancellationToken cancellationToken)
        {
            var car = await this.carRepository.GetAsync(carId, cancellationToken);
            if (car is null)
            {
                return new NotFoundResult();
            }

            var httpContext = this.actionContextAccessor.ActionContext.HttpContext;
            if (httpContext.Request.Headers.TryGetValue(HeaderNames.IfModifiedSince, out var stringValues))
            {
                if (DateTimeOffset.TryParse(stringValues, out var modifiedSince) &&
                    (modifiedSince >= car.Modified))
                {
                    return new StatusCodeResult(StatusCodes.Status304NotModified);
                }
            }

            var carViewModel = this.carMapper.Map(car);
            httpContext.Response.Headers.Add(HeaderNames.LastModified, car.Modified.ToString("R"));
            return new OkObjectResult(carViewModel);
        }
    }
}