class CertRequest
{
    public CertRequest(string account, string[] domains, CertificateData data)
    {
        Account = account;
        Domains = domains;
        Data = data;
    }
    public string Account { get; }
    public string[] Domains { get; }
    public CertificateData Data { get;}
}