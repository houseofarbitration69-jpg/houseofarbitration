#region Imports
using System.Collections.Generic;
#endregion

namespace House.Of.Arbitration.Models;

public class JudgeStateModel
{
    public string? Group { get; set; }
    public int Number { get; set; }
    public string? Name { get; set; }
    public double Score { get; set; }
    public double RedPoints { get; set; }
    public double BluePoints { get; set; }
    public List<MvtTimeCodeModel> Codes { get; set; } = new();
}
