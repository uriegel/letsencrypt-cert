class CertificateData
{
    public CertificateData(string countryName,string state, string locality, string organization,
        string organizationUnit, string commonName)
    {
        CountryName = countryName;
        State = state;
        Locality = locality;
        Organization = organization;
        OrganizationUnit = organizationUnit;
        CommonName = commonName;
    }

    public string CountryName { get; }
    public string State { get; }
    public string Locality { get; }
    public string Organization { get; }
    public string OrganizationUnit { get; }
    public string CommonName { get; }
}