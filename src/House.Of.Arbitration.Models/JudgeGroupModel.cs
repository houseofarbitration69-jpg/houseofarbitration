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
    private double _initialScore = 0.0;
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
    /// Obtient ou définit le score initial du groupe
    /// </summary>
    public double InitialScore
    {
        get => _initialScore;
        set
        {
            if (SetProperty(ref _initialScore, value))
            {
                OnPropertyChanged(nameof(GlobalScore));
            }
        }
    }

    /// <summary>
    /// Obtient la liste des juges du groupe
    /// </summary>
    public ObservableCollection<JudgeModel> Judges { get; } = new();

    /// <summary>
    /// Obtient le score global du groupe (moyenne des juges arrondie à 2 décimales)
    /// </summary>
    public double GlobalScore => Judges.Count > 0
        ? Math.Round(Judges.Average(j => j.Score), 2)
        : InitialScore;
    #endregion

    public JudgeGroupModel(string name, string description, double initialScore = 0.0)
    {
        Name = name;
        Description = description;
        InitialScore = initialScore;

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
