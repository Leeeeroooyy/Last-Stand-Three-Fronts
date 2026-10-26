namespace LastStand.Map
{
    /// <summary>Сторона конфликта. Dawn — нижний левый угол карты, Dusk — верхний правый.</summary>
    public enum TeamSide
    {
        Neutral = 0,
        Dawn = 1,
        Dusk = 2
    }

    public enum Lane
    {
        None = 0,
        Top = 1,
        Mid = 2,
        Bot = 3
    }

    public enum StructureType
    {
        Tower = 0,
        BarracksMelee = 1,
        BarracksRanged = 2,
        Ancient = 3,
        Fountain = 4,
        Shop = 5,
        Shrine = 6,
        Chest = 7,
        HeroSpawn = 8,
        CreepSpawn = 9
    }
}
