namespace AmazonClientless.Models;

public class Entitlement
{
    public class ProductWrapper
    {
        public class ProductDetailWrapper
        {
            public class ProductDetails
            {
                public string? BackgroundUrl1;
                public string? BackgroundUrl2;
                public string? Publisher;
                public List<string> Screenshots = [];
                public List<string> Videos = [];
            }

            public string? IconUrl;
            public ProductDetails Details = new();
        }

        public string? Asin;
        public int AsinVersion;
        public string ID = "";
        public ProductDetailWrapper ProductDetail = new();
        public string? ProductLine;
        public string? Sku;
        public string Title = "";
        public string? Type;
        public string? VendorId;
    }

    public string? ChannelId;
    public string ID = "";
    public ProductWrapper Product = new();
    public string? State;

    public override string ToString()
    {
        return Product?.Title ?? ID;
    }
}

public class EntitlementsResponse
{
    public List<Entitlement> Entitlements = [];
    public string? NextToken;
}