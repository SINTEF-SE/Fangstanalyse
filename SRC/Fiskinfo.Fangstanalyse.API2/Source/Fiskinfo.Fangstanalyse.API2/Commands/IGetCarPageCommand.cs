using Fiskinfo.Fangstanalyse.API2.ViewModels;
using SintefSecureFramework.AspNetCore;

namespace Fiskinfo.Fangstanalyse.API2.Commands
{
    public interface IGetCarPageCommand : IAsyncCommand<PageOptions>
    {
    }
}