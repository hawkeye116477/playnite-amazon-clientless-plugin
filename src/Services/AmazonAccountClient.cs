using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Web;
using AmazonClientless.Models;
using CommonPlugin;
using Microsoft.Win32;
using Playnite;
using Playnite.WebViews;
using PlayniteMod;

namespace AmazonClientless.Services;

public class AmazonAccountClient(IPlayniteApi api)
{
    private readonly ILogger logger = LogManager.GetLogger<AmazonAccountClient>();

    private const string LoginUrl =
        @"https://www.amazon.com/ap/signin";

    private const string LoginUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) @amzn/aga-electron-platform/1.0.0 Chrome/78.0.3904.130 Electron/7.1.9 Safari/537.36";

    private const string LauncherUserAgent =
        "com.amazon.agslauncher.win/3.0.9782.3";

    public static readonly RetryHandler RetryHandler = new RetryHandler(new HttpClientHandler());
    public static readonly HttpClient HttpClient = new HttpClient(RetryHandler);
    
    public static string EncryptedTokensPath =>
        Path.Combine(Path.Combine(AmazonClientlessPlugin.PlayniteApi.UserDataDir, "tokens_encrypted.json"));

    public async Task LogOut()
    {
        using var webView = api.WebView.CreateView(new WebViewSettings
        {
            WindowWidth = 580,
            WindowHeight = 700,
        });
        await webView.DeleteDomainCookiesAsync(".amazon.com");
        FileSystem.DeleteFile(EncryptedTokensPath);
    }

    public async Task Login()
    {
        var callbackUrl = string.Empty;
        var codeChallenge = GenerateCodeChallenge();
        var deviceSerial = GetMachineGuid().ToString("N");
        var clientId = Convert.ToHexString(Encoding.ASCII.GetBytes($"{deviceSerial}#A2UMVHOX7UP4V7")).ToLowerInvariant();
        using var webView = api.WebView.CreateView(new WebViewSettings
        {
            WindowWidth = 490,
            WindowHeight = 660,
            UserAgent = LoginUserAgent,
        });
        webView.LoadingChangedCallbackAsync = async e =>
        {
            var url = webView.GetCurrentAddress();
            if (url.Contains("openid.oa2.authorization_code"))
            {
                callbackUrl = url;
                webView.Close();
            }
        };
        webView.WebViewInitializedCallbackAsync = async _ =>
        {
            await webView.DeleteDomainCookiesAsync(".amazon.com");
            var query = HttpUtility.ParseQueryString("");
            query["openid.ns"] = "http://specs.openid.net/auth/2.0";
            var openidIdentity = "http://specs.openid.net/auth/2.0/identifier_select";
            query["openid.claimed_id"] = openidIdentity;
            query["openid.identity"] = openidIdentity;
            query["openid.mode"] = "checkid_setup";
            query["openid.oa2.scope"] = "device_auth_access";
            query["openid.ns.oa2"] = "http://www.amazon.com/ap/ext/oauth/2";
            query["openid.oa2.response_type"] = "code";
            query["openid.oa2.code_challenge_method"] = "S256";
            query["openid.oa2.client_id"] = $"device:{clientId}";
            query["openid.oa2.code_challenge"] = EncodeBase64Url(codeChallenge.GetSHA256Bytes());
            query["language"] = "en_US";
            query["marketPlaceId"] = "ATVPDKIKX0DER";
            query["openid.return_to"] = "https://www.amazon.com";
            query["openid.pape.max_auth_age"] = "0";
            query["openid.ns.pape"] = "http://specs.openid.net/extensions/pape/1.0";
            var openidAssocHandle = "amzn_sonic_games_launcher";
            query["openid.assoc_handle"] = openidAssocHandle;
            query["pageId"] = openidAssocHandle;
            var lurl = $"{LoginUrl}?{query}";
            webView.Navigate(lurl);
        };
        await webView.OpenDialogAsync();
        if (!callbackUrl.IsNullOrEmpty())
        {
            var rediUri = new Uri(callbackUrl);
            var fragments = HttpUtility.ParseQueryString(rediUri.Query);
            var token = fragments["openid.oa2.authorization_code"];
            if (token != null)
            {
                await Authenticate(token, codeChallenge, clientId);
            }
        }
    }

    private async Task Authenticate(string accessToken, string codeChallenge, string clientId)
    {
        var reqData = new DeviceRegistrationRequest
        {
            Auth_data =
            {
                Authorization_code = accessToken,
                Client_domain = "DeviceLegacy",
                Client_id = clientId,
                Code_algorithm = "SHA-256",
                Code_verifier = codeChallenge,
                Use_global_authentication = false,
            },
            Registration_data =
            {
                App_name = "AGSLauncher for Windows",
                App_version = "1.0.0",
                Device_model = "Windows",
                Device_serial = GetMachineGuid().ToString("N"),
                Device_type = "A2UMVHOX7UP4V7",
                Domain = "Device",
                Os_version = Environment.OSVersion.Version.ToString(4)
            },
            Requested_extensions = ["customer_info", "device_info"],
            Requested_token_type = ["bearer", "mac_dms"]
        };

        var authPostContent = Serialization.ToJson(reqData, true);

        var request = new HttpRequestMessage(HttpMethod.Post, @"https://api.amazon.com/auth/register")
        {
            Content = new StringContent(authPostContent, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("User-Agent", "AGSLauncher/1.0.0");

        try
        {
            using var authResponse = await HttpClient.SendAsync(request);
            authResponse.EnsureSuccessStatusCode();
            var authResponseContent = await authResponse.Content.ReadAsStringAsync();
            var authData = Serialization.FromJson<DeviceRegistrationResponse>(authResponseContent);
            if (authData?.Response?.Success != null)
            {
                authData.Response.Success.Tokens.Bearer.Token_obtain_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var finalResponse = Serialization.ToJson(authData.Response.Success);
                Directory.CreateDirectory(Path.GetDirectoryName(EncryptedTokensPath)!);
                Encryption.EncryptToFile(EncryptedTokensPath,
                    finalResponse,
                    Encoding.UTF8,
                    WindowsIdentity.GetCurrent().User!.Value);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to authenticate with Amazon");
        }
    }

    public async Task<List<Entitlement>> GetAccountEntitlements()
    {
        if (!await GetIsUserLoggedIn())
        {
            throw new Exception("User is not authenticated.");
        }

        var entitlements = new List<Entitlement>();
        var tokens = LoadTokens();
        string? nextToken = null;
        var reqData = new EntitlementsRequest
        {
            // not sure what key this is but it's some key from Amazon.Fuel.Plugin.Entitlement.dll
            KeyId = "d5dc8b8b-86c8-4fc4-ae93-18c0def5314d",
            HardwareHash = Guid.NewGuid().ToString("N")
        };

        do
        {
            reqData.NextToken = nextToken;
            var strCont = new StringContent(Serialization.ToJson(reqData, true), Encoding.UTF8, "application/json");
            strCont.Headers.TryAddWithoutValidation("Expect", "100-continue");
            strCont.Headers.ContentEncoding.Add("amz-1.0");

            using var request = new HttpRequestMessage(HttpMethod.Post, @"https://gaming.amazon.com/api/distribution/entitlements");
            request.Content = strCont;
            request.Headers.Add("User-Agent", LauncherUserAgent);
            request.Headers.Add("X-Amz-Target",
                "com.amazon.animusdistributionservice.entitlement.AnimusEntitlementsService.GetEntitlements");
            request.Headers.Add("x-amzn-token", tokens?.Tokens.Bearer.Access_token);

            try
            {
                using var entlsResponse = await HttpClient.SendAsync(request);
                entlsResponse.EnsureSuccessStatusCode();

                var entlsResponseContent = await entlsResponse.Content.ReadAsStringAsync();
                var entlsData = Serialization.FromJson<EntitlementsResponse>(entlsResponseContent);
                nextToken = entlsData?.NextToken;
                if (entlsData?.Entitlements.HasItems() == true)
                {
                    entitlements.AddRange(entlsData.Entitlements);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to get account entitlements");
            }
        } while (!nextToken.IsNullOrEmpty());

        return entitlements;
    }

    public string GetUsername()
    {
        var tokens = LoadTokens();
        var username = "";
        if (tokens != null)
        {
            if (!tokens.Extensions.Customer_info.Given_name.IsNullOrEmpty())
            {
                username = tokens.Extensions.Customer_info.Given_name;
            }
        }

        return username;
    }

    private DeviceRegistrationResponse.ResponseWrapper.SuccessWrapper? LoadTokens()
    {
        if (File.Exists(EncryptedTokensPath))
        {
            try
            {
                return Serialization.FromJson<DeviceRegistrationResponse.ResponseWrapper.SuccessWrapper>(Encryption.DecryptFromFile(
                    EncryptedTokensPath, Encoding.UTF8,
                    WindowsIdentity.GetCurrent().User?.Value!));
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to load saved tokens.");
            }
        }

        return null;
    }

    public async Task<DeviceRegistrationResponse.ResponseWrapper.SuccessWrapper?> RefreshTokens()
    {
        var tokens = LoadTokens();
        if (tokens != null)
        {
            var tokenLastUpdateTime = new DateTime();
            if (File.Exists(EncryptedTokensPath))
            {
                tokenLastUpdateTime = File.GetLastWriteTime(EncryptedTokensPath);
            }

            var tokenExpirySeconds = tokens.Tokens.Bearer.Expires_in;
            DateTime tokenExpiryTime = tokenLastUpdateTime.AddSeconds(tokenExpirySeconds);
            if (DateTime.Now > tokenExpiryTime)
            {
                var reqData = new TokenRefreshRequest
                {
                    App_name = "AGSLauncher",
                    App_version = "3.0.9495.3",
                    Source_token = tokens.Tokens.Bearer.Refresh_token,
                    Requested_token_type = "access_token",
                    Source_token_type = "refresh_token"
                };

                var authPostContent = Serialization.ToJson(reqData, true);
                var strcont = new StringContent(authPostContent, Encoding.UTF8, "application/json");
                strcont.Headers.TryAddWithoutValidation("Expect", "100-continue");

                try
                {
                    var authResponse = await HttpClient.PostAsync(@"https://api.amazon.com/auth/token",
                        strcont);
                    var authResponseContent = await authResponse.Content.ReadAsStringAsync();
                    logger.Debug(authResponseContent);
                    var authData =
                        Serialization.FromJson<DeviceRegistrationResponse.ResponseWrapper.SuccessWrapper.Bearer>(authResponseContent);
                    if (authData != null)
                    {
                        tokens.Tokens.Bearer.Access_token = authData.Access_token;
                        tokens.Tokens.Bearer.Token_obtain_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    }

                    var jsonTokens = Serialization.ToJson(tokens);
                    Encryption.EncryptToFile(EncryptedTokensPath,
                        jsonTokens,
                        Encoding.UTF8,
                        WindowsIdentity.GetCurrent().User?.Value!);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to renew tokens: {ex}");
                }
            }
        }

        return tokens;
    }


    public async Task<bool> GetIsUserLoggedIn()
    {
        var tokens = await RefreshTokens();
        if (tokens == null)
        {
            return false;
        }

        try
        {
            var infoRequest = new HttpRequestMessage(HttpMethod.Get, @"https://api.amazon.com/user/profile");
            infoRequest.Headers.Add("User-Agent", "AGSLauncher/1.0.0");
            infoRequest.Headers.Add("Authorization", "bearer " + tokens.Tokens.Bearer.Access_token);
            infoRequest.Headers.Add("Accept", "application/json");
            using var infoResponse = await HttpClient.SendAsync(infoRequest);
            var infoResponseContent = await infoResponse.Content.ReadAsStringAsync();
            var infoData = Serialization.FromJson<ProfileInfo>(infoResponseContent);
            return infoData != null && !infoData.User_id.IsNullOrEmpty();
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to check Amazon login status. Error: {ex}");
            return false;
        }
    }

    public static Guid GetMachineGuid()
    {
        RegistryKey root;
        if (Environment.Is64BitOperatingSystem)
        {
            root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }
        else
        {
            root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        }

        try
        {
            using var cryptography = root.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography");
            return Guid.Parse((string)cryptography?.GetValue("MachineGuid")! ?? "");
        }
        finally
        {
            root.Dispose();
        }
    }

    private string EncodeBase64Url(byte[] input)
    {
        return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string GenerateCodeChallenge()
    {
        const string randomStringChars = "ABCDEFGHIJKLMNOPQRSTYVWXZabcdefghijklmnopqrstyvwxz0123456789_";
        var randomSetLeng = randomStringChars.Length - 1;
        var random = new Random();
        var result = new StringBuilder(45);
        for (var i = 0; i < 45; i++)
        {
            result.Append(randomStringChars[random.Next(0, randomSetLeng)]);
        }

        return result.ToString();
    }
}