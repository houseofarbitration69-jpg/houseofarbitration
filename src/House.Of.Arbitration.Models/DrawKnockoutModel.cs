namespace House.Of.Arbitration.Models;

public class DrawKnockoutModel
{
    public int Id { get; set; } = 0;

    public int Order { get; set; } = 0;
    public int GlobalOrder { get; set; } = 0;

    public int DrawId { get; set; } = 0;
    public required DrawModel Draw { get; set; }

    public int? Competitor1Id { get; set; }
    public CompetitorModel? Competitor1 { get; set; }

    public int? Competitor2Id { get; set; }
    public CompetitorModel? Competitor2 { get; set; }

    public int? WinnerId { get; set; }
    public CompetitorModel? Winner { get; set; }  

    public int? LooserId { get; set; }
    public CompetitorModel? Looser { get; set; }

    public List<RefereeDataModel>? RefereeDatas { get; set; }
}
