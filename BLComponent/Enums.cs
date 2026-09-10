namespace BLComponent;

public enum PlayerRole
{
    DeputySheriff,
    Outlaw,
    Renegade,
    Sheriff,
}

public enum CardName
{
    Bang,
    Beer,
    Missed,
    Panic,
    GeneralStore,
    Indians,
    Duel,
    Gatling,
    CatBalou,
    Saloon,
    Stagecoach,
    WellsFargo,
    Barrel,
    Scope,
    Mustang,
    Dynamite,
    BeerBarrel,
    Jail,
    Volcanic,
    Schofield,
    Remington,
    Carabine,
    Winchester
}

public enum CardSuit
{
    Diamonds,
    Hearts,
    Spades,
    Clubs
}

public enum CardRank
{
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

public enum CardType
{
    Instant,
    Equipment,
    Weapon
}

public enum CardRc
{
    Ok,
    TooFar,
    CantPlay,
    SheriffWin,
    OutlawWin,
    RenegadeWin,
    CantEndTurn,
    WaitConditions,
    NeedTargetPlayer,
    NeedTargetCard,
    NoWait,
    WrongPlayer,
    WrongParameter
}
