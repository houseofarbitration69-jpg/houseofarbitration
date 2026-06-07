namespace House.Of.Arbitration.Controls;

/// <summary>
/// Un contrôle de carte personnalisable avec des options d'ombre et de marge interne (padding).
/// </summary>
public partial class Card : ContentView
{
    #region Bindable Properties
    /// <summary>
    /// Identifie la propriété de liaison <see cref="CardColor"/>.
    /// </summary>
    public static readonly BindableProperty CardColorProperty = BindableProperty.Create
    (
        nameof(CardColor),
        typeof(Color),
        typeof(Card),
        Colors.White
    );

    /// <summary>
    /// Obtient ou définit la couleur de fond de la carte.
    /// </summary>
    public Color CardColor
    {
        get => (Color)GetValue(CardColorProperty);
        set => SetValue(CardColorProperty, value);
    }


    /// <summary>
    /// Identifie la propriété de liaison <see cref="CardPadding"/>.
    /// </summary>
    public static readonly BindableProperty CardPaddingProperty = BindableProperty.Create
    (
        nameof(CardPadding),
        typeof(Thickness),
        typeof(Card),
        new Thickness(15)
    );

    /// <summary>
    /// Obtient ou définit la marge interne (padding) à l'intérieur de la carte.
    /// </summary>
    public Thickness CardPadding
    {
        get => (Thickness)GetValue(CardPaddingProperty);
        set => SetValue(CardPaddingProperty, value);
    }

    /// <summary>
    /// Identifie la propriété de liaison <see cref="HasShadow"/>.
    /// </summary>
    public static readonly BindableProperty HasShadowProperty = BindableProperty.Create
    (
        nameof(HasShadow),
        typeof(bool),
        typeof(Card),
        true
    );

    /// <summary>
    /// Obtient ou définit une valeur indiquant si la carte affiche une ombre.
    /// </summary>
    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Initialise une nouvelle instance de la classe <see cref="Card"/>.
    /// </summary>
    public Card()
	{
		InitializeComponent();
	}
	#endregion
}
