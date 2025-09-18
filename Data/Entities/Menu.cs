namespace Authenticate.Data.Entities
{
    public class Menu
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Target { get; set; }
        public int Weight { get; set; }
        public bool IsActive { get; set; }

    }
}
