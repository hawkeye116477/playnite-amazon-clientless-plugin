namespace AmazonClientless.Models;

public class DeviceRegistrationRequest
{
    public class RegistrationData
    {
        public string? App_name;
        public string? App_version;
        public string? Device_model;
        public string? Device_name;
        public string? Device_serial;
        public string? Device_type;
        public string? Domain;
        public string? Os_version;
    }

    public class AuthData
    {
        public string? Authorization_code;
        public string? Client_id;
        public string? Client_domain;
        public bool Use_global_authentication = false;
        public string? Code_verifier;
        public string? Code_algorithm;
    }

    public class UserContextMap
    {
    }

    public RegistrationData Registration_data = new RegistrationData();
    public AuthData Auth_data = new AuthData();
    public UserContextMap User_context_map = new UserContextMap();
    public List<string> Requested_extensions = [];
    public List<string> Requested_token_type = [];
}

public class DeviceRegistrationResponse
{
    public class ResponseWrapper
    {
        public class SuccessWrapper
        {
            public class TokensWrapper
            {
                public MacDms Mac_dms { get; set; } = new();
                public Bearer Bearer { get; set; } = new();
            }

            public class MacDms
            {
                public string? Device_private_key { get; set; }
            }

            public class Bearer
            {
                public string? Access_token { get; set; }
                public string? Refresh_token { get; set; }
                public long Expires_in { get; set; }
                public long Token_obtain_time { get; set; }
            }

            public class ExtensionsWrapper
            {
                public DeviceInfo Device_info { get; set; } = new();
                public CustomerInfo Customer_info { get; set; } = new();
            }

            public class DeviceInfo
            {
                public string? Device_name { get; set; }
                public string? Device_serial_number { get; set; }
                public string? Device_type { get; set; }
            }

            public class CustomerInfo
            {
                public string? Account_pool { get; set; }
                public string? User_id { get; set; }
                public string? Home_region { get; set; }
                public string? Name { get; set; }
                public string? Given_name { get; set; }
            }

            public TokensWrapper Tokens { get; set; } = new();
            public ExtensionsWrapper Extensions { get; set; } = new();

            public string? Customer_id { get; set; }
        }

        public SuccessWrapper? Success;
    }

    public ResponseWrapper? Response;
}

public class ProfileInfo
{
    public string User_id = "";
}

public class TokenRefreshRequest
{
    public string? Source_token_type;
    public string? Requested_token_type;
    public string? Source_token;
    public string? App_name;
    public string? App_version;
}