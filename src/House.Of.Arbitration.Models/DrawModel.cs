namespace House.Of.Arbitration.Models;

public class DrawModel
{
    public int Id { get; set; } = 0;

    public required int CategoryId { get; set; }

    public required CategoryModel Category { get; set; }

    public List<DrawSandaModel>? DrawSandas { get; set; }

    public List<DrawTaoluModel>? DrawTaolus { get; set; }
}
