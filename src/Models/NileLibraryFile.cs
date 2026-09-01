namespace AmazonClientless.Models;

    public class NileLibraryFile
    {
        public List<NileGames> Games { get; set; } = [];

        public class NileGames
        {
            public string ID { get; set; } = "";
            public Product Product { get; set; } = new();
        }

        public class Product
        {
            public int AsinVersion { get; set; }
            public string Description { get; set; } = "";
            public string DomainId { get; set; } = "";
            public string ID { get; set; } = "";
            public string ProductLine { get; set; } = "";
            public string Sku { get; set; } = "";
            public string Title { get; set; } = "";
            public string VendorId { get; set; } = "";
        }
    }