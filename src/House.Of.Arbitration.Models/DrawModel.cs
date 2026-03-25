namespace House.Of.Arbitration.Models;

public class DrawModel
{
    public int Id { get; set; } = 0;

    public required int CategoryId { get; set; }

    public CategoryModel? Category { get; set; }

    public List<DrawKnockoutModel>? DrawKnockouts { get; set; }

    public List<DrawPoolsModel>? DrawPools { get; set; }
    
    public List<DrawOrderModel>? DrawOrders { get; set; }
}
