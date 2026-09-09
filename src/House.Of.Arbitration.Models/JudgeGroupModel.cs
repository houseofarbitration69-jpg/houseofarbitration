#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
#endregion

namespace House.Of.Arbitration.Models;

public partial class JudgeGroupModel : ObservableObject
{
    #region Attributs
    private string _name = string.Empty;
    private string _description = string.Empty;
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit le nom du groupe
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Obtient ou définit la description du groupe
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Obtient la liste des juges du groupe
    /// </summary>
    public ObservableCollection<JudgeModel> Judges { get; } = new();

    /// <summary>
    /// Obtient le score du groupe
    /// </summary>
    public double GlobalScore => Judges.Sum(j => j.Score);
    #endregion

    public JudgeGroupModel(string name, string description)
    {
        Name = name;
        Description = description;

        Judges.CollectionChanged += (s, e) => 
        {
            if (e.NewItems != null)
            {
                foreach (JudgeModel judge in e.NewItems)
                {
                    judge.PropertyChanged += Judge_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (JudgeModel judge in e.OldItems)
                {
                    judge.PropertyChanged -= Judge_PropertyChanged;
                }
            }
            OnPropertyChanged(nameof(GlobalScore));
        };
    }

    private void Judge_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JudgeModel.Score))
        {
            OnPropertyChanged(nameof(GlobalScore));
        }
    }
}
