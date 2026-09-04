#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class JudgeModel : ObservableObject
{
    #region Attributs
    private double _redPoints;
    private double _bluePoints;
    private double _score;
    private bool _isConnected;
    private string _name = string.Empty;
    private int _number;
    private string? _group;
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit le groupe du juge (ex: A, B, C)
    /// </summary>
    public string? Group
    {
        get => _group;
        set => SetProperty(ref _group, value);
    }
    /// <summary>
    /// Obtient ou définit les points rouge
    /// </summary>
    public double RedPoints
    {
        get => _redPoints;
        set => SetProperty(ref _redPoints, value);
    }

    /// <summary>
    /// Obtient ou définit les points bleu
    /// </summary>
    public double BluePoints
    {
        get => _bluePoints;
        set => SetProperty(ref _bluePoints, value);
    }

    /// <summary>
    /// Obtient ou définit si c'est connecté
    /// </summary>
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    /// <summary>
    /// Obtient ou définit le nom du jueg
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Obtient ou définit le nombre
    /// </summary>
    public int Number 
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }

    public double Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }
    #endregion
}
