#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class MvtCodeModel : ObservableObject
{
    public int Id { get; set; }

    public string Code { get; set; } = String.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string Movement { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double Value { get; set; } = 0.1;

    public string Category { get; set; } = string.Empty;

    public int? MvtTypeId { get; set; }

    public MvtTypeModel? Type { get; set; }

    public int? MvtGroupId { get; set; }

    public MvtGroupModel? Group { get; set; }

    /// <summary>
    /// Libellé du groupe affiché (GroupName ou Group.Label ou Category)
    /// </summary>
    public string GroupLabel => !string.IsNullOrWhiteSpace(GroupName) 
        ? GroupName 
        : (!string.IsNullOrWhiteSpace(Group?.Label) ? Group!.Label : (!string.IsNullOrWhiteSpace(Category) ? Category : "Général"));

    /// <summary>
    /// Liste standard des groupes de mouvements Taolu
    /// </summary>
    public static readonly List<string> StandardGroups = new()
    {
        "Techniques de main",
        "Equilibres",
        "Techniques de jambe",
        "Techniques de saut",
        "Positions",
        "Techniques spécifiques aux armes",
        "Autres erreurs"
    };

    /// <summary>
    /// Liste statique des codes de mouvements par défaut
    /// </summary>
    public static List<MvtCodeModel> DefaultCodes => new()
    {
        // 1. Techniques de main
        new MvtCodeModel { Id = 1, Code = "01", GroupName = "Techniques de main", Movement = "Quan (Poing)", Description = "Forme du poing non conforme ou mal serré", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 2, Code = "02", GroupName = "Techniques de main", Movement = "Zhang (Paume)", Description = "Forme de la paume non conforme ou doigts écartés", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 3, Code = "03", GroupName = "Techniques de main", Movement = "Gou Shou (Crochet)", Description = "Crochet non formé ou poignet cassé", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 4, Code = "04", GroupName = "Techniques de main", Movement = "Jian Zhi (Doigts de l'épée)", Description = "Index et majeur non alignés ou mal tenus", Value = 0.1, Category = "Taolu" },

        // 2. Equilibres
        new MvtCodeModel { Id = 5, Code = "10", GroupName = "Equilibres", Movement = "Ban Tui Chao Tian", Description = "Jambe levée non verticale ou instabilité", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 6, Code = "11", GroupName = "Equilibres", Movement = "Hou Tui Bao Jiao", Description = "Jambe arrière trop basse ou manque de stabilité", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 7, Code = "12", GroupName = "Equilibres", Movement = "Yang Shen Ping Heng", Description = "Corps pas assez horizontal ou déséquilibre", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 8, Code = "13", GroupName = "Equilibres", Movement = "Shi Zhi Ping Heng", Description = "Bras ou jambe non alignés", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 9, Code = "14", GroupName = "Equilibres", Movement = "Kou Tui Ping Heng", Description = "Pied non accroché au jarret ou hauteur insuffisante", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 10, Code = "17", GroupName = "Equilibres", Movement = "Ti Xi Du Li", Description = "Genou levé sous la ligne des hanches ou durée < 2s", Value = 0.1, Category = "Taolu" },

        // 3. Techniques de jambe
        new MvtCodeModel { Id = 11, Code = "20", GroupName = "Techniques de jambe", Movement = "Qian Sao Tui (Balayage avant)", Description = "Balayage incomplet (< 360°) ou corps relevé", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 12, Code = "21", GroupName = "Techniques de jambe", Movement = "Hou Sao Tui (Balayage arrière)", Description = "Balayage arrière incomplet ou arrêt", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 13, Code = "22", GroupName = "Techniques de jambe", Movement = "Die Chu Cha (Grand écart)", Description = "Cuisse non au sol ou bassin tourné", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 14, Code = "23", GroupName = "Techniques de jambe", Movement = "Deng Tui (Coup de talon)", Description = "Hauteur insuffisante ou manque de poussée", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 15, Code = "24", GroupName = "Techniques de jambe", Movement = "Zheng Ti Tui (Coup de pied frontal)", Description = "Pied ne monte pas au front ou jambe pliée", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 16, Code = "25", GroupName = "Techniques de jambe", Movement = "Dan Pai Jiao (Coup explosif)", Description = "Frappe non claquée ou hauteur insuffisante", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 17, Code = "26", GroupName = "Techniques de jambe", Movement = "Bai Lian Pai Jiao (Coup extérieur)", Description = "Frappe manquée ou amplitude insuffisante", Value = 0.1, Category = "Taolu" },

        // 4. Techniques de saut
        new MvtCodeModel { Id = 18, Code = "30", GroupName = "Techniques de saut", Movement = "Teng Kong Fei Jiao", Description = "Frappe du pied manquée ou atterrissage instable", Value = 0.2, Category = "Taolu" },
        new MvtCodeModel { Id = 19, Code = "31", GroupName = "Techniques de saut", Movement = "Xuan Feng Jiao (Tornade)", Description = "Rotation insuffisante ou déséquilibre à la réception", Value = 0.2, Category = "Taolu" },
        new MvtCodeModel { Id = 20, Code = "32", GroupName = "Techniques de saut", Movement = "Teng Kong Bai Lian (Lotus)", Description = "Rotation incomplète ou frappe de pied manquée", Value = 0.2, Category = "Taolu" },
        new MvtCodeModel { Id = 21, Code = "33", GroupName = "Techniques de saut", Movement = "Ce Kong Fan (Roue sans mains)", Description = "Mains touchent le sol ou atterrissage fléchi", Value = 0.2, Category = "Taolu" },
        new MvtCodeModel { Id = 22, Code = "34", GroupName = "Techniques de saut", Movement = "Xuan Zi (Saut papillon)", Description = "Corps non horizontal ou atterrissage désordonné", Value = 0.2, Category = "Taolu" },
        new MvtCodeModel { Id = 23, Code = "35", GroupName = "Techniques de saut", Movement = "Xuan Zi Zhuan Ti (Papillon vrillé)", Description = "Vrille incomplète ou chute à la réception", Value = 0.3, Category = "Taolu" },

        // 5. Positions
        new MvtCodeModel { Id = 24, Code = "50", GroupName = "Positions", Movement = "Gong Bu (Position arc)", Description = "Cuisse avant pas horizontale ou talon arrière décollé", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 25, Code = "51", GroupName = "Positions", Movement = "Ma Bu (Position cavalière)", Description = "Cuisse pas horizontale ou genoux rentrés vers l'intérieur", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 26, Code = "52", GroupName = "Positions", Movement = "Xu Bu (Position vide)", Description = "Poids mal placé ou talon avant posé au sol", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 27, Code = "53", GroupName = "Positions", Movement = "Pu Bu (Position rase)", Description = "Pied arrière décollé ou jambe pas tendue", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 28, Code = "54", GroupName = "Positions", Movement = "Xie Bu (Position croisée)", Description = "Genou touche le sol ou fesses relevées", Value = 0.1, Category = "Taolu" },

        // 6. Techniques spécifiques aux armes
        new MvtCodeModel { Id = 29, Code = "60", GroupName = "Techniques spécifiques aux armes", Movement = "Gua Jian / Liao Jian (Épée)", Description = "Trajectoire de l'arme non verticale ou loin du corps", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 30, Code = "61", GroupName = "Techniques spécifiques aux armes", Movement = "Wo Jian (Tenue d'épée)", Description = "Mauvaise prise de l'épée ou garde instable", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 31, Code = "62", GroupName = "Techniques spécifiques aux armes", Movement = "Chan Tou / Guo Nao (Sabre)", Description = "Le sabre ne contourne pas le cou/dos de près", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 32, Code = "63", GroupName = "Techniques spécifiques aux armes", Movement = "Lan Qiang / Na Qiang (Lance)", Description = "Mouvement circulaire incomplet ou manque de rectitude", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 33, Code = "64", GroupName = "Techniques spécifiques aux armes", Movement = "Ping Lun Gun (Bâton)", Description = "Rotation non parallèle au sol", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 34, Code = "66", GroupName = "Techniques spécifiques aux armes", Movement = "Qi Xie Pao Jie (Lancer et réception)", Description = "Lâcher d'arme ou mauvaise réception de l'arme", Value = 0.2, Category = "Taolu" },

        // 7. Autres erreurs
        new MvtCodeModel { Id = 35, Code = "70", GroupName = "Autres erreurs", Movement = "Déséquilibre / Pas supplémentaire", Description = "Pas ou sursaut involontaire de stabilisation", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 36, Code = "71", GroupName = "Autres erreurs", Movement = "Appui involontaire", Description = "Une main ou un genou touche le sol involontairement", Value = 0.2, Category = "Taolu" },
        new MvtCodeModel { Id = 37, Code = "72", GroupName = "Autres erreurs", Movement = "Chute au sol", Description = "Chute ou deux mains/fesses au sol", Value = 0.3, Category = "Taolu" },
        new MvtCodeModel { Id = 38, Code = "73", GroupName = "Autres erreurs", Movement = "Arme touche le sol", Description = "L'arme heurte le sol de manière involontaire", Value = 0.1, Category = "Taolu" },
        new MvtCodeModel { Id = 39, Code = "74", GroupName = "Autres erreurs", Movement = "Perte de l'arme", Description = "L'arme échappe complètement des mains", Value = 0.4, Category = "Taolu" },
        new MvtCodeModel { Id = 40, Code = "75", GroupName = "Autres erreurs", Movement = "Sortie du tapis", Description = "Un ou deux pieds sortent de la surface de compétition", Value = 0.1, Category = "Taolu" }
    };
}

/// <summary>
/// Mode de saisie pour l'arbitrage Taolu
/// </summary>
public enum TaoluInputMode
{
    Code,
    Points,
    GroupCodes
}

