using Avis.Faults;

namespace Avis.Station;

/// <summary>
/// Blocking operator popup for faults the operator has to act on (a service is
/// down): shows the fault and returns only once the operator hits Reset, so the
/// failed step is retried right after. Implemented by the WinForms UI.
/// </summary>
public interface IOperatorAlerts
{
    Task ShowAndWaitForResetAsync(FaultCode code, string message, CancellationToken ct);
}
