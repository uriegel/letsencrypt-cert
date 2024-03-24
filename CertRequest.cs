record CertificateData(
    string CountryName,
    string State,
    string Locality,
    string Organization,
    string OrganizationUnit,
    string CommonName
);

record CertRequest(
    string Account,
    string[] Domains,
    CertificateData Data
);
