namespace AmazonClientless.Models;

public class EntitlementsRequest
{
    public string Operation = "GetEntitlements";
    public string ClientId = "Sonic";
    public int SyncPoint = 0;
    public string? NextToken;
    public int MaxResults = 500;
    public string? KeyId;
    public string? HardwareHash;
    public string? ProductIdFilter;
    public bool DisableStateFilter = true;
}