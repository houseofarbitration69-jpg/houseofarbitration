using System.Collections.ObjectModel;

namespace House.Of.Arbitration.Models;

public class DrawGroup : ObservableCollection<IDrawModel>
{
    public string CategoryName { get; private set; }

    public DrawGroup(string categoryName, IEnumerable<IDrawModel> draws) : base(draws)
    {
        CategoryName = categoryName;
    }
}
