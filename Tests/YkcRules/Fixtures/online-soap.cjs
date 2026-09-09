// Local UI test fixture only. Never configure this endpoint in deployed environments.
const http = require('node:http');
const xml = `<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body><YS_CihazBilgileriGetirResponse xmlns="http://tempuri.org/">
    <YS_CihazBilgileriGetirResult>
      <HataKodu>0</HataKodu><cariad>YEREL TEST MUSTERISI</cariad>
      <adres>Test Mahallesi, Test Sokak No: 1 / Corum</adres>
      <tesisatno>9000001</tesisatno><sozlesmeno>900001</sozlesmeno>
      <carikod>90001</carikod><sayacno>9000011</sayacno>
      <Cihazlar>
        <CihazDto><cihaztipi>Kombi</cihaztipi><cihazmarka>PRIVATE_SOURCE_BRAND</cihazmarka><cihazkapasite>20000</cihazkapasite><projeno>90001</projeno><tesisatno>9000001</tesisatno></CihazDto>
        <CihazDto><cihaztipi>Ocak</cihaztipi><cihazmarka>PRIVATE_SOURCE_BRAND</cihazmarka><cihazkapasite>7740</cihazkapasite><projeno>90001</projeno><tesisatno>9000001</tesisatno></CihazDto>
      </Cihazlar>
    </YS_CihazBilgileriGetirResult>
  </YS_CihazBilgileriGetirResponse></s:Body>
</s:Envelope>`;
http.createServer((req, res) => {
    if (req.method !== 'POST' || req.url !== '/fixture') {
        res.writeHead(404).end();
        return;
    }
    req.resume();
    req.on('end', () => {
        res.writeHead(200, { 'Content-Type': 'text/xml; charset=utf-8' });
        res.end(xml);
    });
}).listen(55193, '127.0.0.1', () => console.log('Synthetic SOAP fixture: http://127.0.0.1:55193/fixture'));
