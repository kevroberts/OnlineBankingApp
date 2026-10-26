namespace OnlineBankingAPI.Data;

/// <summary>Fixed ids for seeded data, so tests and demo scripts can reference them.</summary>
public static class SeedIds
{
    public static readonly Guid PayeeClearingAccount = new("00000000-0000-0000-0000-00000000c1ea");

    public static class Alice
    {
        public static readonly Guid Customer = new("a11ce000-0000-4000-8000-000000000001");
        public static readonly Guid Checking = new("a11ce000-0000-4000-8000-0000000000a1");
        public static readonly Guid Savings = new("a11ce000-0000-4000-8000-0000000000a2");
        public static readonly Guid Landlord = new("a11ce000-0000-4000-8000-0000000000b1");
        public static readonly Guid CityPower = new("a11ce000-0000-4000-8000-0000000000b2");
        public static readonly Guid Mom = new("a11ce000-0000-4000-8000-0000000000b3");
        public static readonly Guid Gym = new("a11ce000-0000-4000-8000-0000000000b4");
    }

    public static class Ben
    {
        public static readonly Guid Customer = new("be000000-0000-4000-8000-000000000002");
        public static readonly Guid Checking = new("be000000-0000-4000-8000-0000000000a1");
        public static readonly Guid Savings = new("be000000-0000-4000-8000-0000000000a2");
        public static readonly Guid HolidaySavings = new("be000000-0000-4000-8000-0000000000a3");
        public static readonly Guid PhoneCo = new("be000000-0000-4000-8000-0000000000b1");
        public static readonly Guid WaterUtility = new("be000000-0000-4000-8000-0000000000b2");
        public static readonly Guid Sister = new("be000000-0000-4000-8000-0000000000b3");
    }

    public static class Chloe
    {
        public static readonly Guid Customer = new("c1010e00-0000-4000-8000-000000000003");
        public static readonly Guid Checking = new("c1010e00-0000-4000-8000-0000000000a1");
        public static readonly Guid Savings = new("c1010e00-0000-4000-8000-0000000000a2");
        public static readonly Guid Daycare = new("c1010e00-0000-4000-8000-0000000000b1");
        public static readonly Guid CarLoan = new("c1010e00-0000-4000-8000-0000000000b2");
    }
}
