namespace House.Of.Arbitration.Models;

public class DrawTaoluModel
{
    public int Id { get; set; } = 0;

    public int DrawId { get; set; } = 0;
    public required DrawModel Draw { get; set; }

    public List<RefereeDataModel>? RefereeDatas { get; set; }
}
