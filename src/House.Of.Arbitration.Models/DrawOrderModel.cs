namespace House.Of.Arbitration.Models;

public class DrawOrderModel
{
    public int Id { get; set; } = 0;
    public int Order { get; set; } = 0;
    public int GlobalOrder { get; set; } = 0;

    public int? CompetitorId { get; set; }
    public CompetitorModel? Competitor { get; set; }

    public int DrawId { get; set; } = 0;
    public required DrawModel Draw { get; set; }

    public List<RefereeDataModel>? RefereeDatas { get; set; }
}
