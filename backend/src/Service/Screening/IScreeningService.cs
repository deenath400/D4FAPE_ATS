namespace Ats.Service.Screening;

using System.Threading;
using System.Threading.Tasks;

public interface IScreeningService
{
    Task<ScreeningResult> EvaluateAsync(
        string requisitionTitle,
        string requisitionDescription,
        string cvText,
        CancellationToken ct = default);
}
