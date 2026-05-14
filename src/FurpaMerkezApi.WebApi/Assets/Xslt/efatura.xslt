<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
    xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
    xmlns:ccts="urn:un:unece:uncefact:documentation:2"
    xmlns:clm54217="urn:un:unece:uncefact:codelist:specification:54217:2001"
    xmlns:clm5639="urn:un:unece:uncefact:codelist:specification:5639:1988"
    xmlns:clm66411="urn:un:unece:uncefact:codelist:specification:66411:2001"
    xmlns:clmIANAMIMEMediaType="urn:un:unece:uncefact:codelist:specification:IANAMIMEMediaType:2003"
    xmlns:fn="http://www.w3.org/2005/xpath-functions" xmlns:link="http://www.xbrl.org/2003/linkbase"
    xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
    xmlns:qdt="urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2"
    xmlns:udt="urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2"
    xmlns:xbrldi="http://xbrl.org/2006/xbrldi" xmlns:xbrli="http://www.xbrl.org/2003/instance"
    xmlns:xdt="http://www.w3.org/2005/xpath-datatypes" xmlns:xlink="http://www.w3.org/1999/xlink"
    xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsd="http://www.w3.org/2001/XMLSchema"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    exclude-result-prefixes="cac cbc ccts clm54217 clm5639 clm66411 clmIANAMIMEMediaType fn link n1 qdt udt xbrldi xbrli xdt xlink xs xsd xsi">
  <xsl:character-map name="a">
    <xsl:output-character character="&#133;" string=""/>
    <xsl:output-character character="&#158;" string=""/>
        <xsl:output-character character="&#129;" string=""/>
        <xsl:output-character character="&#130;" string=""/>
        <xsl:output-character character="&#131;" string=""/>
        <xsl:output-character character="&#132;" string=""/>
        <xsl:output-character character="&#133;" string=""/>
        <xsl:output-character character="&#134;" string=""/>
        <xsl:output-character character="&#135;" string=""/>
        <xsl:output-character character="&#136;" string=""/>
        <xsl:output-character character="&#137;" string=""/>
        <xsl:output-character character="&#138;" string=""/>
        <xsl:output-character character="&#139;" string=""/>
        <xsl:output-character character="&#140;" string=""/>
        <xsl:output-character character="&#141;" string=""/>
        <xsl:output-character character="&#142;" string=""/>
        <xsl:output-character character="&#143;" string=""/>
        <xsl:output-character character="&#144;" string=""/>
        <xsl:output-character character="&#145;" string=""/>
        <xsl:output-character character="&#146;" string=""/>
        <xsl:output-character character="&#147;" string=""/>
        <xsl:output-character character="&#148;" string=""/>
        <xsl:output-character character="&#149;" string=""/>
        <xsl:output-character character="&#150;" string=""/>
        <xsl:output-character character="&#151;" string=""/>
        <xsl:output-character character="&#152;" string=""/>
        <xsl:output-character character="&#153;" string=""/>
        <xsl:output-character character="&#154;" string=""/>
        <xsl:output-character character="&#155;" string=""/>
        <xsl:output-character character="&#156;" string=""/>
        <xsl:output-character character="&#157;" string=""/>
        <xsl:output-character character="&#158;" string=""/>
        <xsl:output-character character="&#159;" string=""/>
  </xsl:character-map>
  <xsl:decimal-format name="european" decimal-separator="," grouping-separator="." NaN=""/>
  <xsl:output version="4.0" method="html" indent="no" encoding="UTF-8"
        doctype-public="-//W3C//DTD HTML 4.01 Transitional//EN"
        doctype-system="http://www.w3.org/TR/html4/loose.dtd" use-character-maps="a"/>
  <xsl:param name="SV_OutputFormat" select="'HTML'"/>
  <xsl:variable name="XML" select="/"/>


  <xsl:template match="/">
    <html>
      <head>
        <title/>
        <style type="text/css">
          body {
          background-color: #FFFFFF;
          font-family: 'Tahoma', "Times New Roman", Times, serif;
          font-size: 11px;
          color: #666666;
          }
          h1, h2 {
          padding-bottom: 3px;
          padding-top: 3px;
          margin-bottom: 5px;
          text-transform: uppercase;
          font-family: Arial, Helvetica, sans-serif;
          }
          h1 {
          font-size: 1.4em;
          text-transform:none;
          }
          h2 {
          font-size: 1em;
          color: brown;
          }
          h3 {
          font-size: 1em;
          color: #333333;
          text-align: justify;
          margin: 0;
          padding: 0;
          }
          h4 {
          font-size: 1.1em;
          font-style: bold;
          font-family: Arial, Helvetica, sans-serif;
          color: #000000;
          margin: 0;
          padding: 0;
          }
          hr {
          height:2px;
          color: #000000;
          background-color: #000000;
          border-bottom: 1px solid #000000;
          }
          p, ul, ol {
          margin-top: 1.5em;
          }
          ul, ol {
          margin-left: 3em;
          }
          blockquote {
          margin-left: 3em;
          margin-right: 3em;
          font-style: italic;
          }
          a {
          text-decoration: none;
          color: #70A300;
          }
          a:hover {
          border: none;
          color: #70A300;
          }
          #despatchTable {
          border-collapse:collapse;
          font-size:11px;
          float:right;
          border-color:gray;
          }
          #ettnTable {
          border-collapse:collapse;
          font-size:11px;
          border-color:gray;
          }
          #customerPartyTable {
          border-width: 0px;
          border-spacing:;
          border-style: inset;
          border-color: gray;
          border-collapse: collapse;
          background-color:
          }
          #customerIDTable {
          border-width: 2px;
          border-spacing:;
          border-style: inset;
          border-color: gray;
          border-collapse: collapse;
          background-color:
          }
          #customerIDTableTd {
          border-width: 2px;
          border-spacing:;
          border-style: inset;
          border-color: gray;
          border-collapse: collapse;
          background-color:
          }
          #lineTable {
          border-width:2px;
          border-spacing:;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color:;
          }
          #lineTableTd {
          border-width: 1px;
          padding: 1px;
          border-style: inset;
          border-color: black;
          background-color: white;
          }
          #lineTableTr {
          border-width: 1px;
          padding: 0px;
          border-style: inset;
          border-color: black;
          background-color: white;
          -moz-border-radius:;
          }
          #lineTableDummyTd {
          border-width: 1px;
          border-color:white;
          padding: 1px;
          border-style: inset;
          border-color: black;
          background-color: white;
          }
          #lineTableBudgetTd {
          border-width: 2px;
          border-spacing:0px;
          padding: 1px;
          border-style: inset;
          border-color: black;
          background-color: white;
          -moz-border-radius:;
          }
          #notesTable {
          border-width: 2px;
          border-spacing:;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color:
          }
          #notesTableTd {
          border-width: 0px;
          border-spacing:;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color:
          }
          table {
          border-spacing:0px;
          }
          #budgetContainerTable {
          border-width: 0px;
          border-spacing: 0px;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color:;
          }
          td {
          border-color:gray;
          }
        </style>
        <title>e-Fatura</title>
      </head>
      <body
                style="margin-left=0.6in; margin-right=0.6in; margin-top=0.79in; margin-bottom=0.79in">
        <xsl:for-each select="$XML">
          <table style="border-color:blue; " border="0" cellspacing="0px" width="800"
                        cellpadding="0px">
            <tbody>
              <tr valign="top">
                <td width="40%">
                  <br/>
                  <table align="center" border="0" width="100%">
                    <tbody>



                        <hr/>
                      <tr align="left">
                        <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
                          <td align="left">
                        <span style="font-weight:bold; font-size:15px; ">
                            <xsl:if test="cac:PartyName">
                              <xsl:value-of select="cac:PartyName/cbc:Name"/>
                              <br/>
                            </xsl:if>
                            </span>
                            <xsl:for-each select="cac:Person">
                              <xsl:for-each select="cbc:Title">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:FirstName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:MiddleName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:FamilyName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:NameSuffix">
                                <xsl:apply-templates/>
                              </xsl:for-each>
                            </xsl:for-each>
                          </td>
                        </xsl:for-each>
                      </tr>
                      <tr align="left">
                        <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
                          <td align="left">
                            <xsl:for-each select="cac:PostalAddress">
                            <xsl:for-each select="cbc:District">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:StreetName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:Region">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:PostalZone">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:BuildingName">
                                <xsl:apply-templates/>
                              </xsl:for-each>
                              <xsl:if test="cbc:BuildingNumber">
                                <xsl:text> No:</xsl:text>
                                <xsl:for-each select="cbc:BuildingNumber">
                                  <xsl:apply-templates/>
                                </xsl:for-each>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:if>
                              <br/>
                              <xsl:for-each select="cbc:CitySubdivisionName">
                                <xsl:apply-templates/>
                              </xsl:for-each>
                              <xsl:text>/ </xsl:text>
                              <xsl:for-each select="cbc:CityName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                            </xsl:for-each>
                          </td>
                        </xsl:for-each>
                      </tr>
                      <xsl:if test="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:Telephone or //n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:Telefax">
                        <tr align="left">
                          <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
                            <td align="left">
                              <xsl:for-each select="cac:Contact">
                                <xsl:if test="cbc:Telephone">
                                  <xsl:text>Tel: </xsl:text>
                                  <xsl:for-each select="cbc:Telephone">
                                    <xsl:apply-templates/>
                                  </xsl:for-each>
                                </xsl:if>
                                <xsl:if test="cbc:Telefax">
                                  <xsl:text> Fax: </xsl:text>
                                  <xsl:for-each select="cbc:Telefax">
                                    <xsl:apply-templates/>
                                  </xsl:for-each>
                                </xsl:if>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                            </td>
                          </xsl:for-each>
                        </tr>
                      </xsl:if>
                      <xsl:for-each select="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cbc:WebsiteURI">
                        <tr align="left">
                          <td>
                            <xsl:text>Web Sitesi: </xsl:text>
                            <xsl:value-of select="."/>
                          </td>
                        </tr>
                      </xsl:for-each>
                      <xsl:for-each select="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:ElectronicMail">
                        <tr align="left">
                          <td>
                            <xsl:text>E-Posta: </xsl:text>
                            <xsl:value-of select="."/>
                          </td>
                        </tr>
                      </xsl:for-each>
                      <tr align="left">
                        <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
                          <td align="left">
                            <xsl:text>Vergi Dairesi: </xsl:text>
                            <xsl:for-each select="cac:PartyTaxScheme">
                              <xsl:for-each select="cac:TaxScheme">
                                <xsl:for-each select="cbc:Name">
                                  <xsl:apply-templates/>
                                </xsl:for-each>
                              </xsl:for-each>
                              <xsl:text>&#160; </xsl:text>
                            </xsl:for-each>
                          </td>
                        </xsl:for-each>
                      </tr>
                      <xsl:for-each select="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification">
                        <tr align="left">
                          <td>
                            <xsl:value-of select="cbc:ID/@schemeID"/>
                            <xsl:text>: </xsl:text>
                            <xsl:value-of select="cbc:ID"/>
                          </td>
                        </tr>
                      </xsl:for-each>
                    </tbody>
                  </table>
                  <hr/>
                </td>

                
                  <td width="20%" align="center" valign="middle">
                  <br/>
                  <br/>
                  <img style="width:91px;" align="middle" alt="E-Fatura Logo"
                                        src="data:image/jpeg;base64,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"/>

                  <h1 align="center">
                    <span style="font-weight:bold; ">
                      <xsl:text>e-FATURA</xsl:text>
                    </span>
                  </h1>
                  </td>
        <td width="40%" align="center" valign="middle">
                <img src="data:image/jpeg;base64,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"/>
              </td>
                     </tr>
              <tr style="height:118px; " valign="top">
                <td width="40%" align="right" valign="bottom">
                  <table id="customerPartyTable" align="left" border="0" height="50%">
                    <tbody>
                      <tr style="height:71px; ">
                        <td>
                          <hr/>
                          <table align="center" border="0">
                                                <tbody>
                                                <tr>
                                                <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                                                    <td style="width:469px; " align="left">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>SAYIN</xsl:text>
                                                        </span>
                                                    </td>
                                                </xsl:for-each>                                                 
                                                </tr>
                                                <tr>
                                                    <xsl:choose>
                                                        <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='TAXFREE']">
                                                            <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                <xsl:call-template name="Party_Title">
                                                                    <xsl:with-param name="PartyType">TAXFREE</xsl:with-param>
                                                                </xsl:call-template>
                                                            </xsl:for-each>                                                         
                                                        </xsl:when>
                                                        <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='EXPORT']">
                                                            <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                <xsl:call-template name="Party_Title">
                                                                    <xsl:with-param name="PartyType">EXPORT</xsl:with-param>
                                                                </xsl:call-template>
                                                            </xsl:for-each>                                                         
                                                        </xsl:when>
                                                        <xsl:otherwise>
                                                            <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                                                                <xsl:call-template name="Party_Title">
                                                                    <xsl:with-param name="PartyType">OTHER</xsl:with-param>
                                                                </xsl:call-template>
                                                            </xsl:for-each>                                                         
                                                        </xsl:otherwise>
                                                    </xsl:choose>                                                   
                                                </tr>
                                                    <xsl:choose>
                                                        <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='TAXFREE']">
                                                                <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                    <tr>
                                                                        <xsl:call-template name="Party_Adress">
                                                                            <xsl:with-param name="PartyType">TAXFREE</xsl:with-param>
                                                                        </xsl:call-template>
                                                                    </tr>
                                                                    <xsl:call-template name="Party_Other">
                                                                        <xsl:with-param name="PartyType">TAXFREE</xsl:with-param>
                                                                    </xsl:call-template>
                                                                </xsl:for-each>                                                         
                                                        </xsl:when>
                                                        <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='EXPORT']">
                                                            <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                <tr>
                                                                    <xsl:call-template name="Party_Adress">
                                                                        <xsl:with-param name="PartyType">EXPORT</xsl:with-param>
                                                                    </xsl:call-template>
                                                                </tr>
                                                                <xsl:call-template name="Party_Other">
                                                                    <xsl:with-param name="PartyType">EXPORT</xsl:with-param>
                                                                </xsl:call-template>
                                                            </xsl:for-each>                                                         
                                                        </xsl:when>
                                                        <xsl:otherwise>
                                                            <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                                                                <tr>
                                                                    <xsl:call-template name="Party_Adress">
                                                                        <xsl:with-param name="PartyType">OTHER</xsl:with-param>                                                                 
                                                                    </xsl:call-template>
                                                                </tr>
                                                                <xsl:call-template name="Party_Other">
                                                                    <xsl:with-param name="PartyType">OTHER</xsl:with-param>
                                                                </xsl:call-template>
                                                            </xsl:for-each>
                                                        </xsl:otherwise>
                                                    </xsl:choose>                                                                                                       
                                                </tbody>
                                                </table>
                          <hr/>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                  <br/>
                </td>
                <td width="60%" align="center" valign="bottom" colspan="2">
                  <table border="1" height="13" id="despatchTable">
                    <tbody>
                      <tr>
                        <td style="width:105px;" align="left">
                          <span style="font-weight:bold; ">
                            <xsl:text>Özelleştirme No:</xsl:text>
                          </span>
                        </td>
                        <td style="width:110px;" align="left">
                          <xsl:for-each select="n1:Invoice/cbc:CustomizationID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>
                      <tr style="height:13px; ">
                        <td align="left">
                          <span style="font-weight:bold; ">
                            <xsl:text>Senaryo:</xsl:text>
                          </span>
                        </td>
                        <td align="left">
                          <xsl:for-each select="n1:Invoice/cbc:ProfileID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>
                      <tr style="height:13px; ">
                        <td align="left">
                          <span style="font-weight:bold; ">
                            <xsl:text>Fatura Tipi:</xsl:text>
                          </span>
                        </td>
                        <td align="left">
                          <xsl:for-each select="n1:Invoice/cbc:InvoiceTypeCode">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>
                      <tr style="height:13px; ">
                        <td align="left">
                          <span style="font-weight:bold; ">
                            <xsl:text>Fatura No:</xsl:text>
                          </span>
                        </td>
                        <td align="left">
                          <xsl:for-each select="n1:Invoice/cbc:ID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>
                      <tr style="height:13px; ">
                        <td align="left">
                          <span style="font-weight:bold; ">
                            <xsl:text>Fatura Tarihi:</xsl:text>
                          </span>
                        </td>
                        <td align="left">
                          <xsl:for-each select="n1:Invoice/cbc:IssueDate">
                            <xsl:apply-templates select="."/>
                          </xsl:for-each>
                        </td>
                      </tr>
                      <tr style="height:13px; ">
                        <td align="left">
                          <span style="font-weight:bold; ">
                            <xsl:text>Fatura Zamanı:</xsl:text>
                          </span>
                        </td>
                        <td align="left">
                          <xsl:for-each select="n1:Invoice">
                            <xsl:for-each select="cbc:IssueTime">
                              <xsl:apply-templates/>
                            </xsl:for-each>
                          </xsl:for-each>
                        </td>
                      </tr>
                      <xsl:for-each select="n1:Invoice/cac:DespatchDocumentReference">
                        <tr style="height:13px; ">
                          <td align="left">
                            <span style="font-weight:bold; ">
                              <xsl:text>İrsaliye No:</xsl:text>
                            </span>
                            <xsl:text>&#160;</xsl:text>
                          </td>
                          <td align="left">
                            <xsl:value-of select="cbc:ID"/>
                          </td>
                        </tr>
                        <tr style="height:13px; ">
                          <td align="left">
                            <span style="font-weight:bold; ">
                              <xsl:text>İrsaliye Tarihi:</xsl:text>
                            </span>
                          </td>
                          <td align="left">
                            <xsl:for-each select="cbc:IssueDate">
                              <xsl:apply-templates select="."/>
                            </xsl:for-each>
                          </td>
                        </tr>
                      </xsl:for-each>
                      <xsl:if test="//n1:Invoice/cac:OrderReference">
                        <tr style="height:13px">
                          <td align="left">
                            <span style="font-weight:bold; ">
                              <xsl:text>Sipariş No:</xsl:text>
                            </span>
                          </td>
                          <td align="left">
                            <xsl:for-each select="n1:Invoice/cac:OrderReference/cbc:ID">
                              <xsl:apply-templates/>
                            </xsl:for-each>
                          </td>
                        </tr>
                      </xsl:if>
                      <xsl:if   test="//n1:Invoice/cac:OrderReference/cbc:IssueDate">
                        <tr style="height:13px">
                          <td align="left">
                            <span style="font-weight:bold; ">
                              <xsl:text>Sipariş Tarihi:</xsl:text>
                            </span>
                          </td>
                          <td align="left">
                            <xsl:for-each select="n1:Invoice/cac:OrderReference/cbc:IssueDate">
                              <xsl:apply-templates select="."/>
                            </xsl:for-each>
                          </td>
                        </tr>
                    
                      </xsl:if>
                        <xsl:if
                                                  
                                             test="//n1:Invoice/cac:PaymentMeans/cbc:PaymentDueDate">
                            <tr style="height:13px">
                                <td align="left">
                                    <span style="font-weight:bold; ">
                                        <xsl:text>Vade Tarihi:</xsl:text>
                                    </span>
                                </td>
                                <td align="left">
                                    <xsl:for-each
                                    select="n1:Invoice/cac:PaymentMeans">
                                        <xsl:for-each select="cbc:PaymentDueDate">
                                            <xsl:value-of select="substring(.,9,2)"
                                                />-<xsl:value-of select="substring(.,6,2)"
                                                />-<xsl:value-of select="substring(.,1,4)"/>
                                        </xsl:for-each>
                                    </xsl:for-each>
                                </td>
                            </tr>
                        </xsl:if>



                        <xsl:for-each select="n1:Invoice/cac:TaxRepresentativeParty/cac:PartyIdentification/cbc:ID[@schemeID='ARACIKURUMVKN']">
                        <tr>
                          <td style="width:105px;" align="left">
                            <span style="font-weight:bold; ">
                              <xsl:text>Aracı Kurum VKN:</xsl:text>
                            </span>
                          </td>
                          <td style="width:110px;" align="left">
                            <xsl:value-of select="."/>
                          </td>
                        </tr>
                        <tr>
                          <td style="width:105px;" align="left">
                            <span style="font-weight:bold; ">
                              <xsl:text>Aracı Kurum Unvan:</xsl:text>
                            </span>
                          </td>
                          <td style="width:110px;" align="left">
                            <xsl:value-of select="../../cac:PartyName/cbc:Name"/>
                          </td>
                        </tr>
                      </xsl:for-each>
                    </tbody>
                  </table>
                </td>
              </tr>
              <tr align="left">
                <table id="ettnTable">
                  <tr style="height:13px;">
                    <td align="left" valign="top">
                      <span style="font-weight:bold; ">
                        <xsl:text>ETTN:</xsl:text>
                      </span>
                    </td>
                    <td align="left" width="240px">
                      <xsl:for-each select="n1:Invoice/cbc:UUID">
                        <xsl:apply-templates/>
                      </xsl:for-each>
                    </td>
                  </tr>
                </table>
              </tr>
            </tbody>
          </table>
          <div id="lineTableAligner">
            <span>
              <xsl:text>&#160;</xsl:text>
            </span>
          </div>
          <table border="1" id="lineTable" width="800">
            <tbody>
              <tr id="lineTableTr">
                <td id="lineTableTd" style="width:3%">
                  <span style="font-weight:bold; " align="center">
                    <xsl:text>Sıra No</xsl:text>
                  </span>
                </td>
                <td id="lineTableTd" style="width:10%" align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>Mal Hizmet Kodu</xsl:text>
                  </span>
                </td>
                  
                  <td id="lineTableTd" style="width:20%" align="center">
                                    <span style="font-weight:bold; ">
                                        <xsl:text>Mal Hizmet Adı</xsl:text>
                                    </span>
                                </td>
                            

                <td id="lineTableTd" style="width:7.4%" align="center">
                  <span style="font-weight:bold;">
                    <xsl:text>Miktar</xsl:text>
                  </span>
                </td>
                <td id="lineTableTd" style="width:9%" align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>Birim Fiyat</xsl:text>
                  </span>
                </td>
                <td id="lineTableTd" style="width:7%" align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>İskonto Oranı</xsl:text>
                  </span>
                </td>
                <td id="lineTableTd" style="width:9%" align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>İskonto Tutarı</xsl:text>
                  </span>
                </td>
                <td id="lineTableTd" style="width:7%" align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>KDV Oranı</xsl:text>
                  </span>
                </td>
                <td id="lineTableTd" style="width:10%" align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>KDV Tutarı</xsl:text>
                  </span>
                </td>
                <td id="lineTableTd" style="width:17%; " align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>Diğer Vergiler</xsl:text>
                  </span>
                </td>
                <xsl:if test="//n1:Invoice/cbc:ProfileID='IHRACAT'">
                                    <td id="lineTableTdX" style="width:4%;" align="center">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Teslim Şartı</xsl:text>
                                        </span>
                                    </td>                                   
                                <!--    <td id="lineTableTd" style="width:10%" align="center">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Eşya Kap Cinsi</xsl:text>
                                        </span>
                                    </td>                                   
                                    <td id="lineTableTd" style="width:10%" align="center">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Kap No</xsl:text>
                                        </span>
                                    </td>                                   
                                    <td id="lineTableTd" style="width:10%" align="center">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Kap Adet</xsl:text>
                                        </span>
                                    </td>                                   
                                    <td id="lineTableTd" style="width:10%" align="center">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Teslim/Bedel Ödeme Yeri</xsl:text>
                                        </span>
                                    </td> -->                                   
                                    <td id="lineTableTdX" style="width:6%;" align="center">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Gönderilme Şekli</xsl:text>
                                        </span>
                                    </td>                                   
                                    <td id="lineTableTdX" style="width:10%;" align="center">
                                        <span style="font-weight:bold;">
                                            <xsl:text>GTİP</xsl:text>
                                        </span>
                                    </td>                                   
                                </xsl:if>
                <td id="lineTableTd" style="width:10.6%" align="center">
                  <span style="font-weight:bold; ">
                    <xsl:text>Mal Hizmet Tutarı</xsl:text>
                  </span>
                </td>
              </tr>
              <xsl:if test="count(//n1:Invoice/cac:InvoiceLine) &gt;= 20">
                <xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
                  <xsl:apply-templates select="."/>
                </xsl:for-each>
              </xsl:if>
              <xsl:if test="count(//n1:Invoice/cac:InvoiceLine) &lt; 20">
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[1]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[1]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[2]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[2]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[3]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[3]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[4]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[4]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[5]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[5]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[6]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[6]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[7]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[7]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[8]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[8]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[9]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[9]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[10]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[10]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[11]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[11]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[12]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[12]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[13]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[13]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[14]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[14]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[15]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[15]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[16]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[16]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[17]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[17]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[18]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[18]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[19]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[19]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                  <xsl:when test="//n1:Invoice/cac:InvoiceLine[20]">
                    <xsl:apply-templates
                                            select="//n1:Invoice/cac:InvoiceLine[20]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:Invoice"/>
                  </xsl:otherwise>
                </xsl:choose>
              </xsl:if>
            </tbody>
          </table>
        </xsl:for-each>
        <table id="budgetContainerTable" width="800px">
          <tr id="budgetContainerTr" align="right">
            <td id="budgetContainerDummyTd"/>
            <td id="lineTableBudgetTd" align="right" width="200px">
              <span style="font-weight:bold; ">
                <xsl:text>Mal Hizmet Toplam Tutarı</xsl:text>
              </span>
            </td>
            <td id="lineTableBudgetTd" style="width:81px; " align="right">
              <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount">
                <xsl:call-template name="Curr_Type"/>
              </xsl:for-each>
            </td>
          </tr>
          <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
            <xsl:if test="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode = '4171'">
              <tr id="budgetContainerTr" align="right">
                <td id="budgetContainerDummyTd"/>
                <td id="lineTableBudgetTd" align="right" width="200px">
                  <span style="font-weight:bold; ">
                    <xsl:text>Teslim Bedeli</xsl:text>
                  </span>
                </td>
                <td id="lineTableBudgetTd" style="width:81px; " align="right">
                  <xsl:for-each select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount">
                    <xsl:call-template name="Curr_Type"/>
                  </xsl:for-each>
                </td>
              </tr>
            </xsl:if>
          </xsl:for-each>
          <tr id="budgetContainerTr" align="right">
            <td id="budgetContainerDummyTd"/>
            <td id="lineTableBudgetTd" align="right" width="200px">
              <span style="font-weight:bold; ">
                <xsl:text>Toplam İskonto</xsl:text>
              </span>
            </td>
            <td id="lineTableBudgetTd" style="width:81px; " align="right">
              <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount">
                <xsl:call-template name="Curr_Type"/>
              </xsl:for-each>
            </td>
          </tr>
          <tr id="budgetContainerTr" align="right">
            <td id="budgetContainerDummyTd"/>
            <td id="lineTableBudgetTd" align="right" width="200px">
              <span style="font-weight:bold; ">
                <xsl:text>Toplam Masraf</xsl:text>
              </span>
            </td>
            <td id="lineTableBudgetTd" style="width:81px; " align="right">
              <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:ChargeTotalAmount">
                <xsl:call-template name="Curr_Type"/>
              </xsl:for-each>
            </td>
          </tr>
          <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
            <tr id="budgetContainerTr" align="right">
              <td id="budgetContainerDummyTd"/>
              <td id="lineTableBudgetTd" width="211px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Hesaplanan </xsl:text>
                  <xsl:value-of select="cac:TaxCategory/cac:TaxScheme/cbc:Name"/>
                  <xsl:text>(%</xsl:text>
                  <xsl:value-of select="cbc:Percent"/>
                  <xsl:text>)</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:for-each select="cac:TaxCategory/cac:TaxScheme">
                  <xsl:text> </xsl:text>
                  <xsl:value-of
                                        select="format-number(../../cbc:TaxAmount, '###.##0,00', 'european')"/>
                  <xsl:if test="../../cbc:TaxAmount/@currencyID">
                    <xsl:text> </xsl:text>
                    <xsl:if test="../../cbc:TaxAmount/@currencyID = 'TRL' or ../../cbc:TaxAmount/@currencyID = 'TRY'">
                      <xsl:text>TL</xsl:text>
                    </xsl:if>
                    <xsl:if test="../../cbc:TaxAmount/@currencyID != 'TRL' and ../../cbc:TaxAmount/@currencyID != 'TRY'">
                      <xsl:value-of select="../../cbc:TaxAmount/@currencyID"/>
                    </xsl:if>
                  </xsl:if>
                </xsl:for-each>
              </td>
            </tr>
          </xsl:for-each>
          <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
            <xsl:if test="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode = '4171'">
              <tr id="budgetContainerTr" align="right">
                <td id="budgetContainerDummyTd"/>
                <td id="lineTableBudgetTd" align="right" width="200px">
                  <span style="font-weight:bold; ">
                    <xsl:text>KDV Matrahı</xsl:text>
                  </span>
                </td>
                <td id="lineTableBudgetTd" style="width:81px; " align="right">
                  <xsl:value-of
                                            select="format-number(sum(//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=0015]/cbc:TaxableAmount), '###.##0,00', 'european')"/>
                  <xsl:if
                                        test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID">
                    <xsl:text> </xsl:text>
                    <xsl:if
                                            test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID = 'TRL' or //n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID = 'TRY'">
                      <xsl:text>TL</xsl:text>
                    </xsl:if>
                    <xsl:if
                                            test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID != 'TRL' and //n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID != 'TRY'">
                      <xsl:value-of
                                                select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID"
                                            />
                    </xsl:if>
                  </xsl:if>
                </td>
              </tr>
              <tr id="budgetContainerTr" align="right">
                <td id="budgetContainerDummyTd"/>
                <td id="lineTableBudgetTd" align="right" width="200px">
                  <span style="font-weight:bold; ">
                    <xsl:text>Tevkifat Dahil Toplam Tutar</xsl:text>
                  </span>
                </td>
                <td id="lineTableBudgetTd" style="width:81px; " align="right">
                  <xsl:for-each select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount">
                    <xsl:call-template name="Curr_Type"/>
                  </xsl:for-each>
                </td>
              </tr>
              <tr id="budgetContainerTr" align="right">
                <td id="budgetContainerDummyTd"/>
                <td id="lineTableBudgetTd" align="right" width="200px">
                  <span style="font-weight:bold; ">
                    <xsl:text>Tevkifat Hariç Toplam Tutar</xsl:text>
                  </span>
                </td>
                <td id="lineTableBudgetTd" style="width:81px; " align="right">
                  <xsl:for-each select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount">
                    <xsl:call-template name="Curr_Type"/>
                  </xsl:for-each>
                </td>
              </tr>
            </xsl:if>
          </xsl:for-each>
          <xsl:for-each select="n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal">
            <tr id="budgetContainerTr" align="right">
              <td id="budgetContainerDummyTd"/>
              <td id="lineTableBudgetTd" width="211px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Hesaplanan KDV Tevkifat</xsl:text>
                  <xsl:text>(%</xsl:text>
                  <xsl:value-of select="cbc:Percent"/>
                  <xsl:text>)</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:for-each select="cac:TaxCategory/cac:TaxScheme">
                  <xsl:text> </xsl:text>
                  <xsl:value-of
                                        select="format-number(../../cbc:TaxAmount, '###.##0,00', 'european')"/>
                  <xsl:if test="../../cbc:TaxAmount/@currencyID">
                    <xsl:text> </xsl:text>
                    <xsl:if test="../../cbc:TaxAmount/@currencyID = 'TRL' or ../../cbc:TaxAmount/@currencyID = 'TRY'">
                      <xsl:text>TL</xsl:text>
                    </xsl:if>
                    <xsl:if test="../../cbc:TaxAmount/@currencyID != 'TRL' and ../../cbc:TaxAmount/@currencyID != 'TRY'">
                      <xsl:value-of select="../../cbc:TaxAmount/@currencyID"/>
                    </xsl:if>
                  </xsl:if>
                </xsl:for-each>
              </td>
            </tr>
          </xsl:for-each>
          <xsl:if
                        test="sum(n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:TaxableAmount)>0">
            <tr id="budgetContainerTr" align="right">
              <td id="budgetContainerDummyTd"/>
              <td id="lineTableBudgetTd" width="211px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Tevkifata Tabi İşlem Tutarı</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:value-of
                                    select="format-number(sum(n1:Invoice/cac:InvoiceLine[cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:LineExtensionAmount), '###.##0,00', 'european')"/>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL'">
                  <xsl:text>TL</xsl:text>
                </xsl:if>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL'">
                  <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                </xsl:if>
              </td>
            </tr>
            <tr id="budgetContainerTr" align="right">
              <td id="budgetContainerDummyTd"/>
              <td id="lineTableBudgetTd" width="211px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Tevkifata Tabi İşlem Üzerinden Hes. KDV</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:value-of
                                    select="format-number(sum(n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:TaxableAmount), '###.##0,00', 'european')"/>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL'">
                  <xsl:text>TL</xsl:text>
                </xsl:if>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL'">
                  <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                </xsl:if>
              </td>
            </tr>
          </xsl:if>
          <xsl:if test = "n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]">
            <tr id="budgetContainerTr" align="right">
              <td id="budgetContainerDummyTd"/>
              <td id="lineTableBudgetTd" width="211px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Tevkifata Tabi İşlem Tutarı</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:if test = "n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]">
                  <xsl:value-of
                                        select="format-number(sum(n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]/cbc:LineExtensionAmount), '###.##0,00', 'european')"/>
                </xsl:if>
                <xsl:if test = "//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=&apos;9015&apos;">
                  <xsl:value-of
                                        select="format-number(sum(n1:Invoice/cac:InvoiceLine[cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:LineExtensionAmount), '###.##0,00', 'european')"/>
                </xsl:if>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL' or n1:Invoice/cbc:DocumentCurrencyCode = 'TRY'">
                  <xsl:text>TL</xsl:text>
                </xsl:if>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL' and n1:Invoice/cbc:DocumentCurrencyCode != 'TRY'">
                  <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                </xsl:if>
              </td>
            </tr>
            <tr id="budgetContainerTr" align="right">
              <td id="budgetContainerDummyTd"/>
              <td id="lineTableBudgetTd" width="211px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Tevkifata Tabi İşlem Üzerinden Hes. KDV</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:if test = "n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]">
                  <xsl:value-of
                                        select="format-number(sum(n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme]/cbc:TaxableAmount), '###.##0,00', 'european')"/>
                </xsl:if>
                <xsl:if test = "//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=&apos;9015&apos;">
                  <xsl:value-of
                                        select="format-number(sum(n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:TaxableAmount), '###.##0,00', 'european')"/>
                </xsl:if>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL' or n1:Invoice/cbc:DocumentCurrencyCode = 'TRY'">
                  <xsl:text>TL</xsl:text>
                </xsl:if>
                <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL' and n1:Invoice/cbc:DocumentCurrencyCode != 'TRY'">
                  <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                </xsl:if>
              </td>
            </tr>
          </xsl:if>
          <tr id="budgetContainerTr" align="right">
            <td id="budgetContainerDummyTd"/>
            <td id="lineTableBudgetTd" width="200px" align="right">
              <span style="font-weight:bold; ">
                <xsl:text>Vergiler Dahil Toplam Tutar</xsl:text>
              </span>
            </td>
            <td id="lineTableBudgetTd" style="width:82px; " align="right">
              <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount">
                <xsl:call-template name="Curr_Type"/>
              </xsl:for-each>
            </td>
          </tr>
          <tr id="budgetContainerTr" align="right">
            <td id="budgetContainerDummyTd"/>
            <td id="lineTableBudgetTd" width="200px" align="right">
              <span style="font-weight:bold; ">
                <xsl:text>Ödenecek Tutar</xsl:text>
              </span>
            </td>
            <td id="lineTableBudgetTd" style="width:82px; " align="right">
              <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount">
                <xsl:call-template name="Curr_Type"/>
              </xsl:for-each>
            </td>
          </tr>
          <xsl:if
                        test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount/@currencyID != 'TRL' and //n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount/@currencyID != 'TRY'">
            <tr align="right">
              <td/>
              <td id="lineTableBudgetTd" align="right" width="200px">
                <span style="font-weight:bold; ">
                  <xsl:text>Mal Hizmet Toplam Tutarı(TL)</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:81px; " align="right">
                <xsl:value-of
                                    select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
                <xsl:text> TL</xsl:text>
              </td>
            </tr>
            <tr id="budgetContainerTr" align="right">
              <td/>
              <td id="lineTableBudgetTd" width="200px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Vergiler Dahil Toplam Tutar(TL)</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:value-of
                                    select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
                <xsl:text> TL</xsl:text>
              </td>
            </tr>
            <tr align="right">
              <td/>
              <td id="lineTableBudgetTd" width="200px" align="right">
                <span style="font-weight:bold; ">
                  <xsl:text>Ödenecek Tutar(TL)</xsl:text>
                </span>
              </td>
              <td id="lineTableBudgetTd" style="width:82px; " align="right">
                <xsl:value-of
                                    select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
                <xsl:text> TL</xsl:text>
              </td>
            </tr>
          </xsl:if>
        </table>
        <br/>
        <table><tr><td>
        <table id="notesTable" width="800" align="left" height="100">
          <tbody>
            <tr align="left">
              <td id="notesTableTd">
                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode != 'IHRACKAYITLI'">
                  <xsl:for-each select="//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
                    <xsl:if test="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015' and cac:TaxCategory/cbc:TaxExemptionReason">
                      <b>&#160;&#160;&#160;&#160;&#160; Vergi İstisna Muafiyet Sebebi: </b>
                      <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReasonCode"/>
                      <xsl:text>-</xsl:text>
                      <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReason"/>
                      <br/>
                    </xsl:if>
                  </xsl:for-each>
                </xsl:if>  
                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode = 'IHRACKAYITLI'">
                  <xsl:for-each select="//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
                    <xsl:if test="cac:TaxCategory/cbc:TaxExemptionReason">
                      <b>&#160;&#160;&#160;&#160;&#160; İhraç Kayıtlı Fatura Sebebi: </b>
                      <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReasonCode"/>
                      <xsl:text>-</xsl:text>
                      <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReason"/>
                      <br/>
                    </xsl:if>
                  </xsl:for-each>
                </xsl:if>
                                <xsl:for-each select="//n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
                                    <b>&#160;&#160;&#160;&#160;&#160; Tevkifat Sebebi: </b>
                                    <xsl:value-of select="cbc:TaxTypeCode"/>
                                    <xsl:text>-</xsl:text>
                                    <xsl:value-of select="cbc:Name"/>
                                    <br/>
                                </xsl:for-each>
                <xsl:if test="//n1:Invoice/cbc:Note">
                  <b>&#160;&#160;&#160;&#160;&#160; Not: </b>
                    <xsl:for-each select="//n1:Invoice/cbc:Note">
                        <xsl:if test="position() > 1">
                            <b>&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;</b>
                        </xsl:if>
                        <xsl:value-of select="."/>
                        <br/>
                    </xsl:for-each>


                    <!-- <xsl:value-of select="//n1:Invoice/cbc:Note"/> -->
                  <br/>
                </xsl:if>
                <xsl:if test="//n1:Invoice/cac:PaymentMeans/cbc:InstructionNote">
                  <b>&#160;&#160;&#160;&#160;&#160; Ödeme Notu: </b>
                  <xsl:value-of
                                        select="//n1:Invoice/cac:PaymentMeans/cbc:InstructionNote"/>
                  <br/>
                </xsl:if>
                <xsl:if
                                    test="//n1:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:PaymentNote">
                  <b>&#160;&#160;&#160;&#160;&#160; Hesap Açıklaması: </b>
                  <xsl:value-of
                                        select="//n1:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:PaymentNote"/>
                  <br/>
                </xsl:if>
                <xsl:if test="//n1:Invoice/cac:PaymentTerms/cbc:Note">
                  <b>&#160;&#160;&#160;&#160;&#160; Ödeme Koşulu: </b>
                  <xsl:value-of select="//n1:Invoice/cac:PaymentTerms/cbc:Note"/>
                  <br/>
                </xsl:if>
              </td>
            </tr>

              <tr>
                  <td>
                      <br/> Fatura 8 gün içinde itiraz edilmezse kabul edilmiş sayılır.
                     
                  </td>
              </tr>
          </tbody>
      </table>
    
      </td></tr>
      <tr><td>
      <table id="notesTable" width="800" align="left" height="70">
          <tbody>
              <tr align="left">
                  <td id="notesTableTd">

                      <div id="staticFooter">

                          <table id="hesapBilgileriTable" style="border-collapse: collapse;font-size: 10px;font-weight: normal;">
                              <tr id="IBANHesapBaslik">
                                  <td id="lineTableTd" colspan='6'>  
      <b>
          <xsl:text>BANKA BİLGİLERİ</xsl:text>
      </b></td>
                                  

                                  <td />
                              </tr>
                              <tr id="IBANHesapBaslik">
                                  <td id="lineTableTd">BANKA</td>
                                  <td id="lineTableTd" style="padding:3px;">ŞUBE</td>
                                  <td id="lineTableTd" style="padding:3px;">ŞUBE KODU</td>
                                  <td id="lineTableTd" style="padding:3px;">HESAP NO</td>
                                  <td id="lineTableTd" style="padding:3px;">PARA BİRİMİ</td>
                                  <td id="lineTableTd" style="padding:3px;">İBAN</td>

                                  <td />
                              </tr>
                              <tr>
                                  <td id="lineTableTd">
                                      <b>Türkiye İş Bankası</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>Sanayi Şubesi</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>2212</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>447257</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TL</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TR84 0006 4000 0012 2120 4472 57</b>
                                  </td>


                              </tr>
                              <tr>
                                  <td id="lineTableTd">
                                      <b>Türk Ekonomi Bankası</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>Bursa Şubesi</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>17</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>48241479</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TL</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TR45 0003 2000 0000 0048 2414 79</b>
                                  </td>




                              </tr>
                              <tr>
                                  <td id="lineTableTd">
                                      <b>Yapı ve Kredi Bankası</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>Setbaşı Şubesi</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>293</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>81313700</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TL</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TR94 0006 7010 0000 0081 3137 00</b>
                                  </td>




                              </tr>
                              <tr>
                                  <td id="lineTableTd">
                                      <b>Türkiye Halk Bankası</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>Fomara Şubesi</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>1352</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>10100341</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TL</b>
                                  </td>
                                  <td id="lineTableTd">
                                      <b>TR76 0001 2001 3520 0010 1003 41</b>
                                  </td>

                              </tr>

                          </table>

                      </div>

                  </td>

              </tr>
          </tbody>
      </table>
      </td></tr></table>
             
      </body>
    </html>
  </xsl:template>
  <xsl:template match="//n1:Invoice/cac:InvoiceLine">
    <xsl:variable name="itemName" select="./cac:Item/cbc:Name" />
    <xsl:variable name="itemPrice" select="./cac:Price" />
    <xsl:variable name="itemSeller" select="./cac:Item/cac:SellersItemIdentification/cbc:ID" />
    <xsl:variable name="mergedLines" select="//cac:InvoiceLine[./cac:Item/cac:SellersItemIdentification/cbc:ID = $itemSeller and number(./cac:Price) = $itemPrice]" />
    <xsl:variable name="tax0015" select="$mergedLines/cac:TaxTotal/    cac:TaxSubtotal[./cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015']/cbc:TaxAmount" />
    <xsl:if test="not(preceding::cac:InvoiceLine[./cac:Item/cac:SellersItemIdentification/cbc:ID = $itemSeller and number(./cac:Price) = $itemPrice])">
      <tr id="lineTableTr">
        <td class="lineTableTd">
          <xsl:text> </xsl:text>
          <xsl:value-of select="./cbc:ID"/>
        </td>
        <td class="lineTableTd">
          <xsl:text></xsl:text>
          <xsl:value-of select="./cac:Item/cac:SellersItemIdentification/cbc:ID" />
        </td>
        <td class="lineTableTd">
          <xsl:text></xsl:text>
          <xsl:value-of select="./cac:Item/cbc:Name" />
        </td>
        <td class="lineTableTd" align="right">
          <xsl:text></xsl:text>
          <xsl:value-of select="format-number(sum($mergedLines/cbc:InvoicedQuantity), '###.###,####', 'european')" />
          <xsl:if test="./cbc:InvoicedQuantity/@unitCode">
            <xsl:for-each select="./cbc:InvoicedQuantity">
              <xsl:text />
              <xsl:choose>
                <xsl:when test="@unitCode  = '2W'">
                  <span>
                    <xsl:text> Bidon</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = '4A'">
                  <span>
                    <xsl:text> Bobin</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = '4B'">
                  <span>
                    <xsl:text> Kap</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = '5H'">
                  <span>
                    <xsl:text> Faz</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'A49'">
                  <span>
                    <xsl:text> Denye</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'A76'">
                  <span>
                    <xsl:text> Gal.</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'AA'">
                  <span>
                    <xsl:text> Top</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'AB'">
                  <span>
                    <xsl:text> Koli</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'ANN'">
                  <span>
                    <xsl:text> Yıl</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'AS'">
                  <span>
                    <xsl:text> Asorti</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'B5'">
                  <span>
                    <xsl:text> Kütük</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'B55'">
                  <span>
                    <xsl:text> KVM</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BAR'">
                  <span>
                    <xsl:text> Bar</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BD'">
                  <span>
                    <xsl:text> Pano</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BG'">
                  <span>
                    <xsl:text> Torba/Poşet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BH'">
                  <span>
                    <xsl:text> Fırça</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BJ'">
                  <span>
                    <xsl:text> Kova</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BK'">
                  <span>
                    <xsl:text> Sepet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BLD'">
                  <span>
                    <xsl:text> Varil</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BO'">
                  <span>
                    <xsl:text> Şişe</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BR'">
                  <span>
                    <xsl:text> Bar</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'BX'">
                  <span>
                    <xsl:text> Kutu</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CA'">
                  <span>
                    <xsl:text> Kutu</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CGM'">
                  <span>
                    <xsl:text> Cgm</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CH'">
                  <span>
                    <xsl:text> Konteyner</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CL'">
                  <span>
                    <xsl:text> Bobin</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CLT'">
                  <span>
                    <xsl:text> CLT</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CMK'">
                  <span>
                    <xsl:text> CM²</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CMQ'">
                  <span>
                    <xsl:text> CM³</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CMT'">
                  <span>
                    <xsl:text> CM</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CS'">
                  <span>
                    <xsl:text> Kutu</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CT'">
                  <span>
                    <xsl:text> Koli</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CU'">
                  <span>
                    <xsl:text> Kupa</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'CY'">
                  <span>
                    <xsl:text> Silindir</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'D61'">
                  <span>
                    <xsl:text> DK</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'D62'">
                  <span>
                    <xsl:text> SN</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'D79'">
                  <span>
                    <xsl:text> Demet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'D92'">
                  <span>
                    <xsl:text> Bant</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'D97'">
                  <span>
                    <xsl:text> Palet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DAY'">
                  <span>
                    <xsl:text> Gün</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DLT'">
                  <span>
                    <xsl:text> DLT</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DMK'">
                  <span>
                    <xsl:text> DM²</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DMT'">
                  <span>
                    <xsl:text> DM</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DPC'">
                  <span>
                    <xsl:text> Düzine</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DPR'">
                  <span>
                    <xsl:text> Düzine</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DR'">
                  <span>
                    <xsl:text> Varil</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DRL'">
                  <span>
                    <xsl:text> Düzine</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DZN'">
                  <span>
                    <xsl:text> Düzine</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DZP'">
                  <span>
                    <xsl:text> Düzine</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'E4'">
                  <span>
                    <xsl:text> Brüt KG</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'EA'">
                  <span>
                    <xsl:text> Beher</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'EV'">
                  <span>
                    <xsl:text> Zarf</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'FOT'">
                  <span>
                    <xsl:text> Ayak</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GB'">
                  <span>
                    <xsl:text> Galon</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GD'">
                  <span>
                    <xsl:text> Brüt Varil</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GLI'">
                  <span>
                    <xsl:text> Galon</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GLL'">
                  <span>
                    <xsl:text> Galon</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GN'">
                  <span>
                    <xsl:text> Gross Galon</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GRM'">
                  <span>
                    <xsl:text> GR</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GRO'">
                  <span>
                    <xsl:text> Brüt</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'GT'">
                  <span>
                    <xsl:text> Gross Ton</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'HA'">
                  <span>
                    <xsl:text> Çile</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'HUR'">
                  <span>
                    <xsl:text> Saat</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'IE'">
                  <span>
                    <xsl:text> Kişi</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'INH'">
                  <span>
                    <xsl:text> İnç</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'K6'">
                  <span>
                    <xsl:text> KLT</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'KGM'">
                  <span>
                    <xsl:text> KG</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'KJO'">
                  <span>
                    <xsl:text> KJO</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'KMK'">
                  <span>
                    <xsl:text> KM²</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'KTM'">
                  <span>
                    <xsl:text> KM</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'KWH'">
                  <span>
                    <xsl:text> KWH</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'KWT'">
                  <span>
                    <xsl:text> KWT</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'LR'">
                  <span>
                    <xsl:text> Tabaka</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'LTR'">
                  <span>
                    <xsl:text> LT</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MGM'">
                  <span>
                    <xsl:text> MGM</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MIN'">
                  <span>
                    <xsl:text> DK</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MLT'">
                  <span>
                    <xsl:text> MLT</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MMQ'">
                  <span>
                    <xsl:text> MM³</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MMT'">
                  <span>
                    <xsl:text> MM</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MON'">
                  <span>
                    <xsl:text> Ay</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MTK'">
                  <span>
                    <xsl:text> MT²</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MTQ'">
                  <span>
                    <xsl:text> MT³</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MTR'">
                  <span>
                    <xsl:text> MT</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'NIU'">
                  <span>
                    <xsl:text> Adet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'NT'">
                  <span>
                    <xsl:text> Net Ton</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'PA'">
                  <span>
                    <xsl:text> Paket</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'PF'">
                  <span>
                    <xsl:text> Palet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'PG'">
                  <span>
                    <xsl:text> Plaka</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'PL'">
                  <span>
                    <xsl:text> Kova</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'PR'">
                  <span>
                    <xsl:text> Çift</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'RD'">
                  <span>
                    <xsl:text> Çubuk</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'RG'">
                  <span>
                    <xsl:text> Halka</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'RL'">
                  <span>
                    <xsl:text> Makara</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'RO'">
                  <span>
                    <xsl:text> Rulo</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'SA'">
                  <span>
                    <xsl:text> Çuval</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'SET'">
                  <span>
                    <xsl:text> Set</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'SO'">
                  <span>
                    <xsl:text> Makara</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'TN'">
                  <span>
                    <xsl:text> Teneke</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'TU'">
                  <span>
                    <xsl:text> Tüp</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'Z3'">
                  <span>
                    <xsl:text> Fıçı</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MWH'">
                  <span>
                    <xsl:text> MWH</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'MAW'">
                  <span>
                    <xsl:text> Megawatt</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'NCR'">
                  <span>
                    <xsl:text> Karat</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = '10'">
                  <span>
                    <xsl:text> Grup</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = '77'">
                  <span>
                    <xsl:text> Miliinç</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = '2P'">
                  <span>
                    <xsl:text> Kilobyte</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'AD'">
                  <span>
                    <xsl:text> Byte</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'Z2'">
                  <span>
                    <xsl:text> Kasa/Sandık</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'ST'">
                  <span>
                    <xsl:text> Sayfa</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'D66'">
                  <span>
                    <xsl:text> Kaset</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DC'">
                  <span>
                    <xsl:text> Disk</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = 'DD'">
                  <span>
                    <xsl:text> Derece</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode  = '26'">
                  <span>
                    <xsl:text> Ton</xsl:text>
                  </span>
                </xsl:when>
                <!--Yeni Kodlar(BEGIN) ubltr.1.2-->
                <xsl:when test="@unitCode = 'C62'">
                  <span>
                    <xsl:text> Adet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode = 'D40'">
                  <span>
                    <xsl:text> Bin Litre</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode = 'H62'">
                  <span>
                    <xsl:text> Yüz Adet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode = 'R9'">
                  <span>
                    <xsl:text> Bin M³</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode = 'T3'">
                  <span>
                    <xsl:text> Bin Adet</xsl:text>
                  </span>
                </xsl:when>
                <xsl:when test="@unitCode = 'TWH'">
                  <span>
                    <xsl:text> Bin KWH</xsl:text>
                  </span>
                </xsl:when>
                <!--Yeni Kodlar(END)-->
              </xsl:choose>
            </xsl:for-each>
          </xsl:if>
        </td>
        <td id="lineTableTd" align="right">
          <xsl:text>&#160;</xsl:text>
          <xsl:value-of
            select="format-number(./cac:Price/cbc:PriceAmount, '###.##0,########', 'european')"/>
          <xsl:if test="./cac:Price/cbc:PriceAmount/@currencyID">
            <xsl:text> </xsl:text>
            <xsl:if test="./cac:Price/cbc:PriceAmount/@currencyID = &quot;TRL&quot; or ./cac:Price/cbc:PriceAmount/@currencyID = &quot;TRY&quot;">
              <xsl:text>TL</xsl:text>
            </xsl:if>
            <xsl:if test="./cac:Price/cbc:PriceAmount/@currencyID != &quot;TRL&quot; and ./cac:Price/cbc:PriceAmount/@currencyID != &quot;TRY&quot;">
              <xsl:value-of select="./cac:Price/cbc:PriceAmount/@currencyID"/>
            </xsl:if>
          </xsl:if>
        </td>
        <td class="lineTableTd" align="right">
          <xsl:text></xsl:text>
          <xsl:for-each select="(./cac:AllowanceCharge[translate(cbc:ChargeIndicator,'abcçdefgğhıijklmnoöpqrsştuüvwxyz','ABCÇDEFGĞHIİJKLMNOÖPQRSŞTUÜVWXYZ') = 'FALSE']/cbc:MultiplierFactorNumeric)[string(number())!='NaN']">
            <xsl:text> %</xsl:text>
            <xsl:value-of select="format-number(. * 100, '###.##0,00', 'european')" />
            <xsl:if test="not(position() = last())">
              <br />
            </xsl:if>
          </xsl:for-each>
        </td>
        <td class="lineTableTd" align="right">
          <xsl:text></xsl:text>
          <xsl:for-each select="(./cac:AllowanceCharge[translate(cbc:ChargeIndicator,'abcçdefgğhıijklmnoöpqrsştuüvwxyz','ABCÇDEFGĞHIİJKLMNOÖPQRSŞTUÜVWXYZ') = 'FALSE']/cbc:Amount)">
          <!--<xsl:variable name="valuePath" select="sum($mergedLines/cac:AllowanceCharge[translate(cbc:ChargeIndicator,'abcçdefgğhıijklmnoöpqrsştuüvwxyz','ABCÇDEFGĞHIİJKLMNOÖPQRSŞTUÜVWXYZ') = 'FALSE']/cbc:Amount)" />-->
          <!--<xsl:value-of select="format-number($valuePath, '###.##0,00', 'european')" />-->
          <xsl:value-of select="format-number(., '###.##0,00', 'european')" />
          <xsl:if test="./cac:TaxTotal/cbc:TaxAmount/@currencyID">
            <xsl:text></xsl:text>
            <xsl:choose>
              <xsl:when test="./cac:TaxTotal/cbc:TaxAmount/@currencyID = 'TRL' or ./cac:TaxTotal/cbc:TaxAmount/@currencyID = 'TRY'">
                <xsl:text>TL</xsl:text>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="./cac:TaxTotal/cbc:TaxAmount/@currencyID" />
              </xsl:otherwise>
            </xsl:choose>
          </xsl:if>
          <xsl:if test="not(position() = last())">
            <br />
          </xsl:if>
           </xsl:for-each>
        </td>
          <td class="lineTableTd" align="right">
            <xsl:text></xsl:text>
            <xsl:for-each select="./cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
              <xsl:if test="cbc:TaxTypeCode='0015' ">
                <xsl:text />
                <xsl:if test="../../cbc:Percent">
                  <xsl:text> %</xsl:text>
                  <xsl:value-of select="format-number(../../cbc:Percent, '###.##0,00', 'european')" />
                </xsl:if>
              </xsl:if>
            </xsl:for-each>
          </td>
          <td class="lineTableTd" align="right">
            <xsl:text> </xsl:text>
            <xsl:if test="$tax0015">
              <xsl:value-of select="format-number(sum($tax0015), '###.##0,00', 'european')" />
              <xsl:if test="./cac:TaxTotal/cbc:TaxAmount">
                <xsl:text></xsl:text>
                <xsl:choose>
                  <xsl:when test="./cac:TaxTotal/cbc:TaxAmount/@currencyID = 'TRL' or ./cac:TaxTotal/cbc:TaxAmount/@currencyID = 'TRY'">
                    <xsl:text>TL</xsl:text>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:value-of select="./cac:TaxTotal/cbc:TaxAmount/@currencyID" />
                  </xsl:otherwise>
                </xsl:choose>
              </xsl:if>
            </xsl:if>
          </td>
          <td class="lineTableTd" style="font-size: xx-small" align="right">
            <xsl:text> </xsl:text>
            <xsl:for-each select="./cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
              <xsl:if test="cbc:TaxTypeCode!='0015' ">
                <xsl:text> </xsl:text>
                <xsl:value-of select="cbc:Name"/>
                <xsl:if test="../../cbc:Percent">
                  <xsl:text> (%</xsl:text>
                  <xsl:value-of select="format-number(../../cbc:Percent, '###.##0,00', 'european')"/>
                  <xsl:text>)=</xsl:text>
                </xsl:if>
                <xsl:for-each select="../../cbc:TaxAmount">
                  <xsl:call-template name="Curr_Typee">
                    <xsl:with-param name="valuePath" select="."/>
                    <xsl:with-param name="format" select="'###.##0,00'"/>
                  </xsl:call-template>
                </xsl:for-each>
                <br/>
              </xsl:if>
            </xsl:for-each>
            <xsl:for-each select="./cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
              <xsl:text> KDV TEVKİFAT</xsl:text>
              <xsl:if test="../../cbc:Percent">
                <xsl:text> (%</xsl:text>
                <xsl:value-of select="format-number(../../cbc:Percent, '###.##0,00', 'european')"/>
                <xsl:text>)=</xsl:text>
              </xsl:if>
              <xsl:for-each select="../../cbc:TaxAmount">
                <xsl:call-template name="Curr_Typee">
                  <xsl:with-param name="valuePath" select="."/>
                  <xsl:with-param name="format" select="'###.##0,00'"/>
                </xsl:call-template>
              </xsl:for-each>
              <xsl:if test="not(position() = last())">
                <br/>
              </xsl:if>
            </xsl:for-each>
          </td>
        <td class="lineTableTd" align="right">
          <xsl:text> </xsl:text>
          <xsl:variable name="valuePath" select="sum($mergedLines/cbc:LineExtensionAmount)" />
          <xsl:value-of select="format-number($valuePath, '###.##0,00', 'european')" />
          <xsl:if test="./cac:TaxTotal/cbc:TaxAmount/@currencyID">
            <xsl:text></xsl:text>
            <xsl:choose>
              <xsl:when test="./cac:TaxTotal/cbc:TaxAmount/@currencyID = 'TRL' or ./cac:TaxTotal/cbc:TaxAmount/@currencyID = 'TRY'">
                <xsl:text>TL</xsl:text>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="./cac:TaxTotal/cbc:TaxAmount/@currencyID" />
              </xsl:otherwise>
            </xsl:choose>
          </xsl:if>
        </td>
      </tr>
    </xsl:if>
  </xsl:template>
  <xsl:template match="//cbc:IssueDate">
    <xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
  </xsl:template>
  <xsl:template match="//n1:Invoice">
    <tr id="lineTableTr">
      <td id="lineTableTd">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td id="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
        <td id="lineTableTd" align="right">
            <xsl:text>&#160;</xsl:text>
        </td>
    <xsl:if test="//n1:Invoice/cbc:ProfileID='IHRACAT'">
                
                
                <td id="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                </td>
                <td id="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                </td> 
                <td id="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                </td>
            </xsl:if>   

    </tr>
  </xsl:template>
  <xsl:template name="Party_Title" >
        <xsl:param name="PartyType" />
        <td style="width:340px; " align="left">
            <xsl:if test="cac:PartyName">
                <xsl:value-of select="cac:PartyName/cbc:Name"/>
                <br/>
            </xsl:if>
            <xsl:for-each select="cac:Person">
                <xsl:for-each select="cbc:Title">
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                <xsl:for-each select="cbc:FirstName">
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                <xsl:for-each select="cbc:MiddleName">
                    <xsl:apply-templates/>
                    <xsl:text>&#160; </xsl:text>
                </xsl:for-each>
                <xsl:for-each select="cbc:FamilyName">
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                <xsl:for-each select="cbc:NameSuffix">
                    <xsl:apply-templates/>
                </xsl:for-each>
                <xsl:if test="$PartyType='TAXFREE'">
                    <br/>
                    <xsl:text>Pasaport No: </xsl:text>
                    <xsl:value-of select="cac:IdentityDocumentReference/cbc:ID"/>
                    <br/>
                    <xsl:text>Ülkesi: </xsl:text>
                    <xsl:for-each select="cbc:NationalityID">
                        <xsl:call-template name="Country">
                            <xsl:with-param name="CountryType"><xsl:value-of select="."/></xsl:with-param>
                        </xsl:call-template>
                    </xsl:for-each>
                </xsl:if>
            </xsl:for-each>
        </td>       
    </xsl:template>
    <xsl:template name="Party_Adress" >
        <xsl:param name="PartyType" />
        <td style="width:340px; " align="left">
        <br/>
            <xsl:for-each select="cac:PostalAddress">
                <xsl:for-each select="cbc:StreetName">
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                <xsl:for-each select="cbc:BuildingName">
                    <xsl:apply-templates/>
                </xsl:for-each>
                <xsl:for-each select="cbc:BuildingNumber">
                    <xsl:text> No:</xsl:text>
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                <br/>
                <xsl:for-each select="cbc:Room">
                    <xsl:text>Kapı No:</xsl:text>
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                
                <xsl:for-each select="cbc:PostalZone">
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                <xsl:for-each select="cbc:CitySubdivisionName">
                    <xsl:apply-templates/>
                    <xsl:text>/ </xsl:text>
                </xsl:for-each>
                <xsl:for-each select="cbc:CityName">
                    <xsl:apply-templates/>
                    <xsl:text>&#160;</xsl:text>
                </xsl:for-each>
                <xsl:if test="$PartyType!='OTHER'">
                    <br/>
                    <xsl:value-of select="cac:Country/cbc:Name"/>
                    <br/>
                </xsl:if>
            </xsl:for-each>
        </td>
    </xsl:template>
    <xsl:template name="TransportMode">
        <xsl:param name="TransportModeType" />
        <xsl:choose>
            <xsl:when test="$TransportModeType=1">Denizyolu</xsl:when>
            <xsl:when test="$TransportModeType=2">Demiryolu</xsl:when>
            <xsl:when test="$TransportModeType=3">Karayolu</xsl:when>
            <xsl:when test="$TransportModeType=4">Havayolu</xsl:when>
            <xsl:when test="$TransportModeType=5">Posta</xsl:when>
            <xsl:when test="$TransportModeType=6">Çok araçlı</xsl:when>
            <xsl:when test="$TransportModeType=7">Sabit taşıma tesisleri</xsl:when>
            <xsl:when test="$TransportModeType=8">İç su taşımacılığı</xsl:when>         
            <xsl:otherwise><xsl:value-of select="$TransportModeType"/></xsl:otherwise>
        </xsl:choose>       
    </xsl:template>
    <xsl:template name="Packaging">
        <xsl:param name="PackagingType" />
        <xsl:choose>
            <xsl:when test="$PackagingType='1A'">Drum, steel</xsl:when>
            <xsl:when test="$PackagingType='1B'">Drum, aluminium</xsl:when>
            <xsl:when test="$PackagingType='1D'">Drum, plywood</xsl:when>
            <xsl:when test="$PackagingType='1F'">Container, flexible</xsl:when>
            <xsl:when test="$PackagingType='1G'">Drum, fibre</xsl:when>
            <xsl:when test="$PackagingType='1W'">Drum, wooden</xsl:when>
            <xsl:when test="$PackagingType='2C'">Barrel, wooden</xsl:when>
            <xsl:when test="$PackagingType='3A'">Jerrican, steel</xsl:when>
            <xsl:when test="$PackagingType='3H'">Jerrican, plastic</xsl:when>
            <xsl:when test="$PackagingType='43'">Bag, super bulk</xsl:when>
            <xsl:when test="$PackagingType='44'">Bag, polybag</xsl:when>
            <xsl:when test="$PackagingType='4A'">Box, steel</xsl:when>
            <xsl:when test="$PackagingType='4B'">Box, aluminium</xsl:when>
            <xsl:when test="$PackagingType='4C'">Box, natural wood</xsl:when>
            <xsl:when test="$PackagingType='4D'">Box, plywood</xsl:when>
            <xsl:when test="$PackagingType='4F'">Box, reconstituted wood</xsl:when>
            <xsl:when test="$PackagingType='4G'">Box, fibreboard</xsl:when>
            <xsl:when test="$PackagingType='4H'">Box, plastic</xsl:when>
            <xsl:when test="$PackagingType='5H'">Bag, woven plastic</xsl:when>
            <xsl:when test="$PackagingType='5L'">Bag, textile</xsl:when>
            <xsl:when test="$PackagingType='5M'">Bag, paper</xsl:when>
            <xsl:when test="$PackagingType='6H'">Composite packaging, plastic receptacle</xsl:when>
            <xsl:when test="$PackagingType='6P'">Composite packaging, glass receptacle</xsl:when>
            <xsl:when test="$PackagingType='7A'">Case, car</xsl:when>
            <xsl:when test="$PackagingType='7B'">Case, wooden</xsl:when>
            <xsl:when test="$PackagingType='8A'">Pallet, wooden</xsl:when>
            <xsl:when test="$PackagingType='8B'">Crate, wooden</xsl:when>
            <xsl:when test="$PackagingType='8C'">Bundle, wooden</xsl:when>
            <xsl:when test="$PackagingType='AA'">Intermediate bulk container, rigid plastic</xsl:when>
            <xsl:when test="$PackagingType='AB'">Receptacle, fibre</xsl:when>
            <xsl:when test="$PackagingType='AC'">Receptacle, paper</xsl:when>
            <xsl:when test="$PackagingType='AD'">Receptacle, wooden</xsl:when>
            <xsl:when test="$PackagingType='AE'">Aerosol</xsl:when>
            <xsl:when test="$PackagingType='AF'">Pallet, modular, collars 80cms * 60cms</xsl:when>
            <xsl:when test="$PackagingType='AG'">Pallet, shrinkwrapped</xsl:when>
            <xsl:when test="$PackagingType='AH'">Pallet, 100cms * 110cms</xsl:when>
            <xsl:when test="$PackagingType='AI'">Clamshell</xsl:when>
            <xsl:when test="$PackagingType='AJ'">Cone</xsl:when>
            <xsl:when test="$PackagingType='AL'">Ball</xsl:when>
            <xsl:when test="$PackagingType='AM'">Ampoule, non-protected</xsl:when>
            <xsl:when test="$PackagingType='AP'">Ampoule, protected</xsl:when>
            <xsl:when test="$PackagingType='AT'">Atomizer</xsl:when>
            <xsl:when test="$PackagingType='AV'">Capsule</xsl:when>
            <xsl:when test="$PackagingType='B4'">Belt</xsl:when>
            <xsl:when test="$PackagingType='BA'">Barrel</xsl:when>
            <xsl:when test="$PackagingType='BB'">Bobbin</xsl:when>
            <xsl:when test="$PackagingType='BC'">Bottlecrate / bottlerack</xsl:when>
            <xsl:when test="$PackagingType='BD'">Board</xsl:when>
            <xsl:when test="$PackagingType='BE'">Bundle</xsl:when>
            <xsl:when test="$PackagingType='BF'">Balloon, non-protected</xsl:when>
            <xsl:when test="$PackagingType='BG'">Bag</xsl:when>
            <xsl:when test="$PackagingType='BH'">Bunch</xsl:when>
            <xsl:when test="$PackagingType='BI'">Bin</xsl:when>
            <xsl:when test="$PackagingType='BJ'">Bucket</xsl:when>
            <xsl:when test="$PackagingType='BK'">Basket</xsl:when>
            <xsl:when test="$PackagingType='BL'">Bale, compressed</xsl:when>
            <xsl:when test="$PackagingType='BM'">Basin</xsl:when>
            <xsl:when test="$PackagingType='BN'">Bale, non-compressed</xsl:when>
            <xsl:when test="$PackagingType='BO'">Bottle, non-protected, cylindrical</xsl:when>
            <xsl:when test="$PackagingType='BP'">Balloon, protected</xsl:when>
            <xsl:when test="$PackagingType='BQ'">Bottle, protected cylindrical</xsl:when>
            <xsl:when test="$PackagingType='BR'">Bar</xsl:when>
            <xsl:when test="$PackagingType='BS'">Bottle, non-protected, bulbous</xsl:when>
            <xsl:when test="$PackagingType='BT'">Bolt</xsl:when>
            <xsl:when test="$PackagingType='BU'">Butt</xsl:when>
            <xsl:when test="$PackagingType='BV'">Bottle, protected bulbous</xsl:when>
            <xsl:when test="$PackagingType='BW'">Box, for liquids</xsl:when>
            <xsl:when test="$PackagingType='BX'">Box</xsl:when>
            <xsl:when test="$PackagingType='BY'">Board, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='BZ'">Bars, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='CA'">Can, rectangular</xsl:when>
            <xsl:when test="$PackagingType='CB'">Crate, beer</xsl:when>
            <xsl:when test="$PackagingType='CC'">Churn</xsl:when>
            <xsl:when test="$PackagingType='CD'">Can, with handle and spout</xsl:when>
            <xsl:when test="$PackagingType='CE'">Creel</xsl:when>
            <xsl:when test="$PackagingType='CF'">Coffer</xsl:when>
            <xsl:when test="$PackagingType='CG'">Cage</xsl:when>
            <xsl:when test="$PackagingType='CH'">Chest</xsl:when>
            <xsl:when test="$PackagingType='CI'">Canister</xsl:when>
            <xsl:when test="$PackagingType='CJ'">Coffin</xsl:when>
            <xsl:when test="$PackagingType='CK'">Cask</xsl:when>
            <xsl:when test="$PackagingType='CL'">Coil</xsl:when>
            <xsl:when test="$PackagingType='CM'">Card</xsl:when>
            <xsl:when test="$PackagingType='CN'">Container, not otherwise specified as transport equipment</xsl:when>
            <xsl:when test="$PackagingType='CO'">Carboy, non-protected</xsl:when>
            <xsl:when test="$PackagingType='CP'">Carboy, protected</xsl:when>
            <xsl:when test="$PackagingType='CQ'">Cartridge</xsl:when>
            <xsl:when test="$PackagingType='CR'">Crate</xsl:when>
            <xsl:when test="$PackagingType='CS'">Case</xsl:when>
            <xsl:when test="$PackagingType='CT'">Carton</xsl:when>
            <xsl:when test="$PackagingType='CU'">Cup</xsl:when>
            <xsl:when test="$PackagingType='CV'">Cover</xsl:when>
            <xsl:when test="$PackagingType='CW'">Cage, roll</xsl:when>
            <xsl:when test="$PackagingType='CX'">Can, cylindrical</xsl:when>
            <xsl:when test="$PackagingType='CY'">Cylinder</xsl:when>
            <xsl:when test="$PackagingType='CZ'">Canvas</xsl:when>
            <xsl:when test="$PackagingType='DA'">Crate, multiple layer, plastic</xsl:when>
            <xsl:when test="$PackagingType='DB'">Crate, multiple layer, wooden</xsl:when>
            <xsl:when test="$PackagingType='DC'">Crate, multiple layer, cardboard</xsl:when>
            <xsl:when test="$PackagingType='DG'">Cage, Commonwealth Handling Equipment Pool (CHEP)</xsl:when>
            <xsl:when test="$PackagingType='DH'">Box, Commonwealth Handling Equipment Pool (CHEP), Eurobox</xsl:when>
            <xsl:when test="$PackagingType='DI'">Drum, iron</xsl:when>
            <xsl:when test="$PackagingType='DJ'">Demijohn, non-protected</xsl:when>
            <xsl:when test="$PackagingType='DK'">Crate, bulk, cardboard</xsl:when>
            <xsl:when test="$PackagingType='DL'">Crate, bulk, plastic</xsl:when>
            <xsl:when test="$PackagingType='DM'">Crate, bulk, wooden</xsl:when>
            <xsl:when test="$PackagingType='DN'">Dispenser</xsl:when>
            <xsl:when test="$PackagingType='DP'">Demijohn, protected</xsl:when>
            <xsl:when test="$PackagingType='DR'">Drum</xsl:when>
            <xsl:when test="$PackagingType='DS'">Tray, one layer no cover, plastic</xsl:when>
            <xsl:when test="$PackagingType='DT'">Tray, one layer no cover, wooden</xsl:when>
            <xsl:when test="$PackagingType='DU'">Tray, one layer no cover, polystyrene</xsl:when>
            <xsl:when test="$PackagingType='DV'">Tray, one layer no cover, cardboard</xsl:when>
            <xsl:when test="$PackagingType='DW'">Tray, two layers no cover, plastic tray</xsl:when>
            <xsl:when test="$PackagingType='DX'">Tray, two layers no cover, wooden</xsl:when>
            <xsl:when test="$PackagingType='DY'">Tray, two layers no cover, cardboard</xsl:when>
            <xsl:when test="$PackagingType='EC'">Bag, plastic</xsl:when>
            <xsl:when test="$PackagingType='ED'">Case, with pallet base</xsl:when>
            <xsl:when test="$PackagingType='EE'">Case, with pallet base, wooden</xsl:when>
            <xsl:when test="$PackagingType='EF'">Case, with pallet base, cardboard</xsl:when>
            <xsl:when test="$PackagingType='EG'">Case, with pallet base, plastic</xsl:when>
            <xsl:when test="$PackagingType='EH'">Case, with pallet base, metal</xsl:when>
            <xsl:when test="$PackagingType='EI'">Case, isothermic</xsl:when>
            <xsl:when test="$PackagingType='EN'">Envelope</xsl:when>
            <xsl:when test="$PackagingType='FB'">Flexibag</xsl:when>
            <xsl:when test="$PackagingType='FC'">Crate, fruit</xsl:when>
            <xsl:when test="$PackagingType='FD'">Crate, framed</xsl:when>
            <xsl:when test="$PackagingType='FE'">Flexitank</xsl:when>
            <xsl:when test="$PackagingType='FI'">Firkin</xsl:when>
            <xsl:when test="$PackagingType='FL'">Flask</xsl:when>
            <xsl:when test="$PackagingType='FO'">Footlocker</xsl:when>
            <xsl:when test="$PackagingType='FP'">Filmpack</xsl:when>
            <xsl:when test="$PackagingType='FR'">Frame</xsl:when>
            <xsl:when test="$PackagingType='FT'">Foodtainer</xsl:when>
            <xsl:when test="$PackagingType='FW'">Cart, flatbed</xsl:when>
            <xsl:when test="$PackagingType='FX'">Bag, flexible container</xsl:when>
            <xsl:when test="$PackagingType='GB'">Bottle, gas</xsl:when>
            <xsl:when test="$PackagingType='GI'">Girder</xsl:when>
            <xsl:when test="$PackagingType='GL'">Container, gallon</xsl:when>
            <xsl:when test="$PackagingType='GR'">Receptacle, glass</xsl:when>
            <xsl:when test="$PackagingType='GU'">Tray, containing horizontally stacked flat items</xsl:when>
            <xsl:when test="$PackagingType='GY'">Bag, gunny</xsl:when>
            <xsl:when test="$PackagingType='GZ'">Girders, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='HA'">Basket, with handle, plastic</xsl:when>
            <xsl:when test="$PackagingType='HB'">Basket, with handle, wooden</xsl:when>
            <xsl:when test="$PackagingType='HC'">Basket, with handle, cardboard</xsl:when>
            <xsl:when test="$PackagingType='HG'">Hogshead</xsl:when>
            <xsl:when test="$PackagingType='HN'">Hanger</xsl:when>
            <xsl:when test="$PackagingType='HR'">Hamper</xsl:when>
            <xsl:when test="$PackagingType='IA'">Package, display, wooden</xsl:when>
            <xsl:when test="$PackagingType='IB'">Package, display, cardboard</xsl:when>
            <xsl:when test="$PackagingType='IC'">Package, display, plastic</xsl:when>
            <xsl:when test="$PackagingType='ID'">Package, display, metal</xsl:when>
            <xsl:when test="$PackagingType='IE'">Package, show</xsl:when>
            <xsl:when test="$PackagingType='IF'">Package, flow</xsl:when>
            <xsl:when test="$PackagingType='IG'">Package, paper wrapped</xsl:when>
            <xsl:when test="$PackagingType='IH'">Drum, plastic</xsl:when>
            <xsl:when test="$PackagingType='IK'">Package, cardboard, with bottle grip-holes</xsl:when>
            <xsl:when test="$PackagingType='IL'">Tray, rigid, lidded stackable (CEN TS 14482:2002)</xsl:when>
            <xsl:when test="$PackagingType='IN'">Ingot</xsl:when>
            <xsl:when test="$PackagingType='IZ'">Ingots, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='JB'">Bag, jumbo</xsl:when>
            <xsl:when test="$PackagingType='JC'">Jerrican, rectangular</xsl:when>
            <xsl:when test="$PackagingType='JG'">Jug</xsl:when>
            <xsl:when test="$PackagingType='JR'">Jar</xsl:when>
            <xsl:when test="$PackagingType='JT'">Jutebag</xsl:when>
            <xsl:when test="$PackagingType='JY'">Jerrican, cylindrical</xsl:when>
            <xsl:when test="$PackagingType='KG'">Keg</xsl:when>
            <xsl:when test="$PackagingType='KI'">Kit</xsl:when>
            <xsl:when test="$PackagingType='LE'">Luggage</xsl:when>
            <xsl:when test="$PackagingType='LG'">Log</xsl:when>
            <xsl:when test="$PackagingType='LT'">Lot</xsl:when>
            <xsl:when test="$PackagingType='LU'">Lug</xsl:when>
            <xsl:when test="$PackagingType='LV'">Liftvan</xsl:when>
            <xsl:when test="$PackagingType='LZ'">Logs, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='MA'">Crate, metal</xsl:when>
            <xsl:when test="$PackagingType='MB'">Bag, multiply</xsl:when>
            <xsl:when test="$PackagingType='MC'">Crate, milk</xsl:when>
            <xsl:when test="$PackagingType='ME'">Container, metal</xsl:when>
            <xsl:when test="$PackagingType='MR'">Receptacle, metal</xsl:when>
            <xsl:when test="$PackagingType='MS'">Sack, multi-wall</xsl:when>
            <xsl:when test="$PackagingType='MT'">Mat</xsl:when>
            <xsl:when test="$PackagingType='MW'">Receptacle, plastic wrapped</xsl:when>
            <xsl:when test="$PackagingType='MX'">Matchbox</xsl:when>
            <xsl:when test="$PackagingType='NA'">Not available</xsl:when>
            <xsl:when test="$PackagingType='NE'">Unpacked or unpackaged</xsl:when>
            <xsl:when test="$PackagingType='NF'">Unpacked or unpackaged, single unit</xsl:when>
            <xsl:when test="$PackagingType='NG'">Unpacked or unpackaged, multiple units</xsl:when>
            <xsl:when test="$PackagingType='NS'">Nest</xsl:when>
            <xsl:when test="$PackagingType='NT'">Net</xsl:when>
            <xsl:when test="$PackagingType='NU'">Net, tube, plastic</xsl:when>
            <xsl:when test="$PackagingType='NV'">Net, tube, textile</xsl:when>
            <xsl:when test="$PackagingType='OA'">Pallet, CHEP 40 cm x 60 cm</xsl:when>
            <xsl:when test="$PackagingType='OB'">Pallet, CHEP 80 cm x 120 cm</xsl:when>
            <xsl:when test="$PackagingType='OC'">Pallet, CHEP 100 cm x 120 cm</xsl:when>
            <xsl:when test="$PackagingType='OD'">Pallet, AS 4068-1993</xsl:when>
            <xsl:when test="$PackagingType='OE'">Pallet, ISO T11</xsl:when>
            <xsl:when test="$PackagingType='OF'">Platform, unspecified weight or dimension</xsl:when>
            <xsl:when test="$PackagingType='OK'">Block</xsl:when>
            <xsl:when test="$PackagingType='OT'">Octabin</xsl:when>
            <xsl:when test="$PackagingType='OU'">Container, outer</xsl:when>
            <xsl:when test="$PackagingType='P2'">Pan</xsl:when>
            <xsl:when test="$PackagingType='PA'">Packet</xsl:when>
            <xsl:when test="$PackagingType='PB'">Pallet, box Combined open-ended box and pallet</xsl:when>
            <xsl:when test="$PackagingType='PC'">Parcel</xsl:when>
            <xsl:when test="$PackagingType='PD'">Pallet, modular, collars 80cms * 100cms</xsl:when>
            <xsl:when test="$PackagingType='PE'">Pallet, modular, collars 80cms * 120cms</xsl:when>
            <xsl:when test="$PackagingType='PF'">Pen</xsl:when>
            <xsl:when test="$PackagingType='PG'">Plate</xsl:when>
            <xsl:when test="$PackagingType='PH'">Pitcher</xsl:when>
            <xsl:when test="$PackagingType='PI'">Pipe</xsl:when>
            <xsl:when test="$PackagingType='PJ'">Punnet</xsl:when>
            <xsl:when test="$PackagingType='PK'">Package</xsl:when>
            <xsl:when test="$PackagingType='PL'">Pail</xsl:when>
            <xsl:when test="$PackagingType='PN'">Plank</xsl:when>
            <xsl:when test="$PackagingType='PO'">Pouch</xsl:when>
            <xsl:when test="$PackagingType='PP'">Piece</xsl:when>
            <xsl:when test="$PackagingType='PR'">Receptacle, plastic</xsl:when>
            <xsl:when test="$PackagingType='PT'">Pot</xsl:when>
            <xsl:when test="$PackagingType='PU'">Tray</xsl:when>
            <xsl:when test="$PackagingType='PV'">Pipes, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='PX'">Pallet</xsl:when>
            <xsl:when test="$PackagingType='PY'">Plates, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='PZ'">Planks, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='QA'">Drum, steel, non-removable head</xsl:when>
            <xsl:when test="$PackagingType='QB'">Drum, steel, removable head</xsl:when>
            <xsl:when test="$PackagingType='QC'">Drum, aluminium, non-removable head</xsl:when>
            <xsl:when test="$PackagingType='QD'">Drum, aluminium, removable head</xsl:when>
            <xsl:when test="$PackagingType='QF'">Drum, plastic, non-removable head</xsl:when>
            <xsl:when test="$PackagingType='QG'">Drum, plastic, removable head</xsl:when>
            <xsl:when test="$PackagingType='QH'">Barrel, wooden, bung type</xsl:when>
            <xsl:when test="$PackagingType='QJ'">Barrel, wooden, removable head</xsl:when>
            <xsl:when test="$PackagingType='QK'">Jerrican, steel, non-removable head</xsl:when>
            <xsl:when test="$PackagingType='QL'">Jerrican, steel, removable head</xsl:when>
            <xsl:when test="$PackagingType='QM'">Jerrican, plastic, non-removable head</xsl:when>
            <xsl:when test="$PackagingType='QN'">Jerrican, plastic, removable head</xsl:when>
            <xsl:when test="$PackagingType='QP'">Box, wooden, natural wood, ordinary</xsl:when>
            <xsl:when test="$PackagingType='QQ'">Box, wooden, natural wood, with sift proof walls</xsl:when>
            <xsl:when test="$PackagingType='QR'">Box, plastic, expanded</xsl:when>
            <xsl:when test="$PackagingType='QS'">Box, plastic, solid</xsl:when>
            <xsl:when test="$PackagingType='RD'">Rod</xsl:when>
            <xsl:when test="$PackagingType='RG'">Ring</xsl:when>
            <xsl:when test="$PackagingType='RJ'">Rack, clothing hanger</xsl:when>
            <xsl:when test="$PackagingType='RK'">Rack</xsl:when>
            <xsl:when test="$PackagingType='RL'">Reel</xsl:when>
            <xsl:when test="$PackagingType='RO'">Roll</xsl:when>
            <xsl:when test="$PackagingType='RT'">Rednet</xsl:when>
            <xsl:when test="$PackagingType='RZ'">Rods, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='SA'">Sack</xsl:when>
            <xsl:when test="$PackagingType='SB'">Slab</xsl:when>
            <xsl:when test="$PackagingType='SC'">Crate, shallow</xsl:when>
            <xsl:when test="$PackagingType='SD'">Spindle</xsl:when>
            <xsl:when test="$PackagingType='SE'">Sea-chest</xsl:when>
            <xsl:when test="$PackagingType='SH'">Sachet</xsl:when>
            <xsl:when test="$PackagingType='SI'">Skid</xsl:when>
            <xsl:when test="$PackagingType='SK'">Case, skeleton</xsl:when>
            <xsl:when test="$PackagingType='SL'">Slipsheet</xsl:when>
            <xsl:when test="$PackagingType='SM'">Sheetmetal</xsl:when>
            <xsl:when test="$PackagingType='SO'">Spool</xsl:when>
            <xsl:when test="$PackagingType='SP'">Sheet, plastic wrapping</xsl:when>
            <xsl:when test="$PackagingType='SS'">Case, steel</xsl:when>
            <xsl:when test="$PackagingType='ST'">Sheet</xsl:when>
            <xsl:when test="$PackagingType='SU'">Suitcase</xsl:when>
            <xsl:when test="$PackagingType='SV'">Envelope, steel</xsl:when>
            <xsl:when test="$PackagingType='SW'">Shrinkwrapped</xsl:when>
            <xsl:when test="$PackagingType='SX'">Set</xsl:when>
            <xsl:when test="$PackagingType='SY'">Sleeve</xsl:when>
            <xsl:when test="$PackagingType='SZ'">Sheets, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='T1'">Tablet</xsl:when>
            <xsl:when test="$PackagingType='TB'">Tub</xsl:when>
            <xsl:when test="$PackagingType='TC'">Tea-chest</xsl:when>
            <xsl:when test="$PackagingType='TD'">Tube, collapsible</xsl:when>
            <xsl:when test="$PackagingType='TE'">Tyre</xsl:when>
            <xsl:when test="$PackagingType='TG'">Tank container, generic</xsl:when>
            <xsl:when test="$PackagingType='TI'">Tierce</xsl:when>
            <xsl:when test="$PackagingType='TK'">Tank, rectangular</xsl:when>
            <xsl:when test="$PackagingType='TL'">Tub, with lid</xsl:when>
            <xsl:when test="$PackagingType='TN'">Tin</xsl:when>
            <xsl:when test="$PackagingType='TO'">Tun</xsl:when>
            <xsl:when test="$PackagingType='TR'">Trunk</xsl:when>
            <xsl:when test="$PackagingType='TS'">Truss</xsl:when>
            <xsl:when test="$PackagingType='TT'">Bag, tote</xsl:when>
            <xsl:when test="$PackagingType='TU'">Tube</xsl:when>
            <xsl:when test="$PackagingType='TV'">Tube, with nozzle</xsl:when>
            <xsl:when test="$PackagingType='TW'">Pallet, triwall</xsl:when>
            <xsl:when test="$PackagingType='TY'">Tank, cylindrical</xsl:when>
            <xsl:when test="$PackagingType='TZ'">Tubes, in bundle/bunch/truss</xsl:when>
            <xsl:when test="$PackagingType='UC'">Uncaged</xsl:when>
            <xsl:when test="$PackagingType='UN'">Unit</xsl:when>
            <xsl:when test="$PackagingType='VA'">Vat</xsl:when>
            <xsl:when test="$PackagingType='VG'">Bulk, gas (at 1031 mbar and 15Â°C)</xsl:when>
            <xsl:when test="$PackagingType='VI'">Vial</xsl:when>
            <xsl:when test="$PackagingType='VK'">Vanpack</xsl:when>
            <xsl:when test="$PackagingType='VL'">Bulk, liquid</xsl:when>
            <xsl:when test="$PackagingType='VO'">Bulk, solid, large particles (Â“nodulesÂ”)</xsl:when>
            <xsl:when test="$PackagingType='VP'">Vacuum-packed</xsl:when>
            <xsl:when test="$PackagingType='VQ'">Bulk, liquefied gas (at abnormal temperature/pressure)</xsl:when>
            <xsl:when test="$PackagingType='VN'">Vehicle</xsl:when>
            <xsl:when test="$PackagingType='VR'">Bulk, solid, granular particles (Â“grainsÂ”)</xsl:when>
            <xsl:when test="$PackagingType='VS'">Bulk, scrap metal</xsl:when>
            <xsl:when test="$PackagingType='VY'">Bulk, solid, fine particles (Â“powdersÂ”)</xsl:when>
            <xsl:when test="$PackagingType='WA'">Intermediate bulk container</xsl:when>
            <xsl:when test="$PackagingType='WB'">Wickerbottle</xsl:when>
            <xsl:when test="$PackagingType='WC'">Intermediate bulk container, steel</xsl:when>
            <xsl:when test="$PackagingType='WD'">Intermediate bulk container, aluminium</xsl:when>
            <xsl:when test="$PackagingType='WF'">Intermediate bulk container, metal</xsl:when>
            <xsl:when test="$PackagingType='WG'">Intermediate bulk container, steel, pressurised > 10 kpa</xsl:when>
            <xsl:when test="$PackagingType='WH'">Intermediate bulk container, aluminium, pressurised > 10 kpa</xsl:when>
            <xsl:when test="$PackagingType='WJ'">Intermediate bulk container, metal, pressure 10 kpa</xsl:when>
            <xsl:when test="$PackagingType='WK'">Intermediate bulk container, steel, liquid</xsl:when>
            <xsl:when test="$PackagingType='WL'">Intermediate bulk container, aluminium, liquid</xsl:when>
            <xsl:when test="$PackagingType='WM'">Intermediate bulk container, metal, liquid</xsl:when>
            <xsl:when test="$PackagingType='WN'">Intermediate bulk container, woven plastic, without coat/liner</xsl:when>
            <xsl:when test="$PackagingType='WP'">Intermediate bulk container, woven plastic, coated</xsl:when>
            <xsl:when test="$PackagingType='WQ'">Intermediate bulk container, woven plastic, with liner</xsl:when>
            <xsl:when test="$PackagingType='WR'">Intermediate bulk container, woven plastic, coated and liner</xsl:when>
            <xsl:when test="$PackagingType='WS'">Intermediate bulk container, plastic film</xsl:when>
            <xsl:when test="$PackagingType='WT'">Intermediate bulk container, textile with out coat/liner</xsl:when>
            <xsl:when test="$PackagingType='WU'">Intermediate bulk container, natural wood, with inner liner</xsl:when>
            <xsl:when test="$PackagingType='WV'">Intermediate bulk container, textile, coated</xsl:when>
            <xsl:when test="$PackagingType='WW'">Intermediate bulk container, textile, with liner</xsl:when>
            <xsl:when test="$PackagingType='WX'">Intermediate bulk container, textile, coated and liner</xsl:when>
            <xsl:when test="$PackagingType='WY'">Intermediate bulk container, plywood, with inner liner</xsl:when>
            <xsl:when test="$PackagingType='WZ'">Intermediate bulk container, reconstituted wood, with inner liner</xsl:when>
            <xsl:when test="$PackagingType='XA'">Bag, woven plastic, without inner coat/liner</xsl:when>
            <xsl:when test="$PackagingType='XB'">Bag, woven plastic, sift proof</xsl:when>
            <xsl:when test="$PackagingType='XC'">Bag, woven plastic, water resistant</xsl:when>
            <xsl:when test="$PackagingType='XD'">Bag, plastics film</xsl:when>
            <xsl:when test="$PackagingType='XF'">Bag, textile, without inner coat/liner</xsl:when>
            <xsl:when test="$PackagingType='XG'">Bag, textile, sift proof</xsl:when>
            <xsl:when test="$PackagingType='XH'">Bag, textile, water resistant</xsl:when>
            <xsl:when test="$PackagingType='XJ'">Bag, paper, multi-wall</xsl:when>
            <xsl:when test="$PackagingType='XK'">Bag, paper, multi-wall, water resistant</xsl:when>
            <xsl:when test="$PackagingType='YA'">Composite packaging, plastic receptacle in steel drum</xsl:when>
            <xsl:when test="$PackagingType='YB'">Composite packaging, plastic receptacle in steel crate box</xsl:when>
            <xsl:when test="$PackagingType='YC'">Composite packaging, plastic receptacle in aluminium drum</xsl:when>
            <xsl:when test="$PackagingType='YD'">Composite packaging, plastic receptacle in aluminium crate</xsl:when>
            <xsl:when test="$PackagingType='YF'">Composite packaging, plastic receptacle in wooden box</xsl:when>
            <xsl:when test="$PackagingType='YG'">Composite packaging, plastic receptacle in plywood drum</xsl:when>
            <xsl:when test="$PackagingType='YH'">Composite packaging, plastic receptacle in plywood box</xsl:when>
            <xsl:when test="$PackagingType='YJ'">Composite packaging, plastic receptacle in fibre drum</xsl:when>
            <xsl:when test="$PackagingType='YK'">Composite packaging, plastic receptacle in fibreboard box</xsl:when>
            <xsl:when test="$PackagingType='YL'">Composite packaging, plastic receptacle in plastic drum</xsl:when>
            <xsl:when test="$PackagingType='YM'">Composite packaging, plastic receptacle in solid plastic box</xsl:when>
            <xsl:when test="$PackagingType='YN'">Composite packaging, glass receptacle in steel drum</xsl:when>
            <xsl:when test="$PackagingType='YP'">Composite packaging, glass receptacle in steel crate box</xsl:when>
            <xsl:when test="$PackagingType='YQ'">Composite packaging, glass receptacle in aluminium drum</xsl:when>
            <xsl:when test="$PackagingType='YR'">Composite packaging, glass receptacle in aluminium crate</xsl:when>
            <xsl:when test="$PackagingType='YS'">Composite packaging, glass receptacle in wooden box</xsl:when>
            <xsl:when test="$PackagingType='YT'">Composite packaging, glass receptacle in plywood drum</xsl:when>
            <xsl:when test="$PackagingType='YV'">Composite packaging, glass receptacle in wickerwork hamper</xsl:when>
            <xsl:when test="$PackagingType='YW'">Composite packaging, glass receptacle in fibre drum</xsl:when>
            <xsl:when test="$PackagingType='YX'">Composite packaging, glass receptacle in fibreboard box</xsl:when>
            <xsl:when test="$PackagingType='YY'">Composite packaging, glass receptacle in expandable plastic pack</xsl:when>
            <xsl:when test="$PackagingType='YZ'">Composite packaging, glass receptacle in solid plastic pack</xsl:when>
            <xsl:when test="$PackagingType='ZA'">Intermediate bulk container, paper, multi-wall</xsl:when>
            <xsl:when test="$PackagingType='ZB'">Bag, large</xsl:when>
            <xsl:when test="$PackagingType='ZC'">Intermediate bulk container, paper, multi-wall, water resistant</xsl:when>
            <xsl:when test="$PackagingType='ZD'">Intermediate bulk container, rigid plastic, with structural equipment, solids</xsl:when>
            <xsl:when test="$PackagingType='ZF'">Intermediate bulk container, rigid plastic, freestanding, solids</xsl:when>
            <xsl:when test="$PackagingType='ZG'">Intermediate bulk container, rigid plastic, with structural equipment, pressurised</xsl:when>
            <xsl:when test="$PackagingType='ZH'">Intermediate bulk container, rigid plastic, freestanding, pressurised</xsl:when>
            <xsl:when test="$PackagingType='ZJ'">Intermediate bulk container, rigid plastic, with structural equipment, liquids</xsl:when>
            <xsl:when test="$PackagingType='ZK'">Intermediate bulk container, rigid plastic, freestanding, liquids</xsl:when>
            <xsl:when test="$PackagingType='ZL'">Intermediate bulk container, composite, rigid plastic, solids</xsl:when>
            <xsl:when test="$PackagingType='ZM'">Intermediate bulk container, composite, flexible plastic, solids</xsl:when>
            <xsl:when test="$PackagingType='ZN'">Intermediate bulk container, composite, rigid plastic, pressurised</xsl:when>
            <xsl:when test="$PackagingType='ZP'">Intermediate bulk container, composite, flexible plastic, pressurised</xsl:when>
            <xsl:when test="$PackagingType='ZQ'">Intermediate bulk container, composite, rigid plastic, liquids</xsl:when>
            <xsl:when test="$PackagingType='ZR'">Intermediate bulk container, composite, flexible plastic, liquids</xsl:when>
            <xsl:when test="$PackagingType='ZS'">Intermediate bulk container, composite</xsl:when>
            <xsl:when test="$PackagingType='ZT'">Intermediate bulk container, fibreboard</xsl:when>
            <xsl:when test="$PackagingType='ZU'">Intermediate bulk container, flexible</xsl:when>
            <xsl:when test="$PackagingType='ZV'">Intermediate bulk container, metal, other than steel</xsl:when>
            <xsl:when test="$PackagingType='ZW'">Intermediate bulk container, natural wood</xsl:when>
            <xsl:when test="$PackagingType='ZX'">Intermediate bulk container, plywood</xsl:when>
            <xsl:when test="$PackagingType='ZY'">Intermediate bulk container, reconstituted wood</xsl:when>
            <xsl:otherwise><xsl:value-of select="$PackagingType"/></xsl:otherwise>
        </xsl:choose>       
    </xsl:template>
    <xsl:template name="Country">
        <xsl:param name="CountryType" />
        <xsl:choose>
            <xsl:when test="$CountryType='AF'">Afganistan</xsl:when>
            <xsl:when test="$CountryType='DE'">Almanya</xsl:when>
            <xsl:when test="$CountryType='AD'">Andorra</xsl:when>
            <xsl:when test="$CountryType='AO'">Angola</xsl:when>
            <xsl:when test="$CountryType='AG'">Antigua ve Barbuda</xsl:when>
            <xsl:when test="$CountryType='AR'">Arjantin</xsl:when>
            <xsl:when test="$CountryType='AL'">Arnavutluk</xsl:when>
            <xsl:when test="$CountryType='AW'">Aruba</xsl:when>
            <xsl:when test="$CountryType='AU'">Avustralya</xsl:when>
            <xsl:when test="$CountryType='AT'">Avusturya</xsl:when>
            <xsl:when test="$CountryType='AZ'">Azerbaycan</xsl:when>
            <xsl:when test="$CountryType='BS'">Bahamalar</xsl:when>
            <xsl:when test="$CountryType='BH'">Bahreyn</xsl:when>
            <xsl:when test="$CountryType='BD'">Bangladeş</xsl:when>
            <xsl:when test="$CountryType='BB'">Barbados</xsl:when>
            <xsl:when test="$CountryType='EH'">Batı Sahra (MA)</xsl:when>
            <xsl:when test="$CountryType='BE'">Belçika</xsl:when>
            <xsl:when test="$CountryType='BZ'">Belize</xsl:when>
            <xsl:when test="$CountryType='BJ'">Benin</xsl:when>
            <xsl:when test="$CountryType='BM'">Bermuda</xsl:when>
            <xsl:when test="$CountryType='BY'">Beyaz Rusya</xsl:when>
            <xsl:when test="$CountryType='BT'">Bhutan</xsl:when>
            <xsl:when test="$CountryType='AE'">Birleşik Arap Emirlikleri</xsl:when>
            <xsl:when test="$CountryType='US'">Birleşik Devletler</xsl:when>
            <xsl:when test="$CountryType='GB'">Birleşik Krallık</xsl:when>
            <xsl:when test="$CountryType='BO'">Bolivya</xsl:when>
            <xsl:when test="$CountryType='BA'">Bosna-Hersek</xsl:when>
            <xsl:when test="$CountryType='BW'">Botsvana</xsl:when>
            <xsl:when test="$CountryType='BR'">Brezilya</xsl:when>
            <xsl:when test="$CountryType='BN'">Bruney</xsl:when>
            <xsl:when test="$CountryType='BG'">Bulgaristan</xsl:when>
            <xsl:when test="$CountryType='BF'">Burkina Faso</xsl:when>
            <xsl:when test="$CountryType='BI'">Burundi</xsl:when>
            <xsl:when test="$CountryType='TD'">Çad</xsl:when>
            <xsl:when test="$CountryType='KY'">Cayman Adaları</xsl:when>
            <xsl:when test="$CountryType='GI'">Cebelitarık (GB)</xsl:when>
            <xsl:when test="$CountryType='CZ'">Çek Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='DZ'">Cezayir</xsl:when>
            <xsl:when test="$CountryType='DJ'">Cibuti</xsl:when>
            <xsl:when test="$CountryType='CN'">Çin</xsl:when>
            <xsl:when test="$CountryType='DK'">Danimarka</xsl:when>
            <xsl:when test="$CountryType='CD'">Demokratik Kongo Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='TL'">Doğu Timor</xsl:when>
            <xsl:when test="$CountryType='DO'">Dominik Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='DM'">Dominika</xsl:when>
            <xsl:when test="$CountryType='EC'">Ekvador</xsl:when>
            <xsl:when test="$CountryType='GQ'">Ekvator Ginesi</xsl:when>
            <xsl:when test="$CountryType='SV'">El Salvador</xsl:when>
            <xsl:when test="$CountryType='ID'">Endonezya</xsl:when>
            <xsl:when test="$CountryType='ER'">Eritre</xsl:when>
            <xsl:when test="$CountryType='AM'">Ermenistan</xsl:when>
            <xsl:when test="$CountryType='MF'">Ermiş Martin (FR)</xsl:when>
            <xsl:when test="$CountryType='EE'">Estonya</xsl:when>
            <xsl:when test="$CountryType='ET'">Etiyopya</xsl:when>
            <xsl:when test="$CountryType='FK'">Falkland Adaları</xsl:when>
            <xsl:when test="$CountryType='FO'">Faroe Adaları (DK)</xsl:when>
            <xsl:when test="$CountryType='MA'">Fas</xsl:when>
            <xsl:when test="$CountryType='FJ'">Fiji</xsl:when>
            <xsl:when test="$CountryType='CI'">Fildişi Sahili</xsl:when>
            <xsl:when test="$CountryType='PH'">Filipinler</xsl:when>
            <xsl:when test="$CountryType='FI'">Finlandiya</xsl:when>
            <xsl:when test="$CountryType='FR'">Fransa</xsl:when>
            <xsl:when test="$CountryType='GF'">Fransız Guyanası (FR)</xsl:when>
            <xsl:when test="$CountryType='PF'">Fransız Polinezyası (FR)</xsl:when>
            <xsl:when test="$CountryType='GA'">Gabon</xsl:when>
            <xsl:when test="$CountryType='GM'">Gambiya</xsl:when>
            <xsl:when test="$CountryType='GH'">Gana</xsl:when>
            <xsl:when test="$CountryType='GN'">Gine</xsl:when>
            <xsl:when test="$CountryType='GW'">Gine Bissau</xsl:when>
            <xsl:when test="$CountryType='GD'">Grenada</xsl:when>
            <xsl:when test="$CountryType='GL'">Grönland (DK)</xsl:when>
            <xsl:when test="$CountryType='GP'">Guadeloupe (FR)</xsl:when>
            <xsl:when test="$CountryType='GT'">Guatemala</xsl:when>
            <xsl:when test="$CountryType='GG'">Guernsey (GB)</xsl:when>
            <xsl:when test="$CountryType='ZA'">Güney Afrika</xsl:when>
            <xsl:when test="$CountryType='KR'">Güney Kore</xsl:when>
            <xsl:when test="$CountryType='GE'">Gürcistan</xsl:when>
            <xsl:when test="$CountryType='GY'">Guyana</xsl:when>
            <xsl:when test="$CountryType='HT'">Haiti</xsl:when>
            <xsl:when test="$CountryType='IN'">Hindistan</xsl:when>
            <xsl:when test="$CountryType='HR'">Hırvatistan</xsl:when>
            <xsl:when test="$CountryType='NL'">Hollanda</xsl:when>
            <xsl:when test="$CountryType='HN'">Honduras</xsl:when>
            <xsl:when test="$CountryType='HK'">Hong Kong (CN)</xsl:when>
            <xsl:when test="$CountryType='VG'">İngiliz Virjin Adaları</xsl:when>
            <xsl:when test="$CountryType='IQ'">Irak</xsl:when>
            <xsl:when test="$CountryType='IR'">İran</xsl:when>
            <xsl:when test="$CountryType='IE'">İrlanda</xsl:when>
            <xsl:when test="$CountryType='ES'">İspanya</xsl:when>
            <xsl:when test="$CountryType='IL'">İsrail</xsl:when>
            <xsl:when test="$CountryType='SE'">İsveç</xsl:when>
            <xsl:when test="$CountryType='CH'">İsviçre</xsl:when>
            <xsl:when test="$CountryType='IT'">İtalya</xsl:when>
            <xsl:when test="$CountryType='IS'">İzlanda</xsl:when>
            <xsl:when test="$CountryType='JM'">Jamaika</xsl:when>
            <xsl:when test="$CountryType='JP'">Japonya</xsl:when>
            <xsl:when test="$CountryType='JE'">Jersey (GB)</xsl:when>
            <xsl:when test="$CountryType='KH'">Kamboçya</xsl:when>
            <xsl:when test="$CountryType='CM'">Kamerun</xsl:when>
            <xsl:when test="$CountryType='CA'">Kanada</xsl:when>
            <xsl:when test="$CountryType='ME'">Karadağ</xsl:when>
            <xsl:when test="$CountryType='QA'">Katar</xsl:when>
            <xsl:when test="$CountryType='KZ'">Kazakistan</xsl:when>
            <xsl:when test="$CountryType='KE'">Kenya</xsl:when>
            <xsl:when test="$CountryType='CY'">Kıbrıs</xsl:when>
            <xsl:when test="$CountryType='KG'">Kırgızistan</xsl:when>
            <xsl:when test="$CountryType='KI'">Kiribati</xsl:when>
            <xsl:when test="$CountryType='CO'">Kolombiya</xsl:when>
            <xsl:when test="$CountryType='KM'">Komorlar</xsl:when>
            <xsl:when test="$CountryType='CG'">Kongo Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='KV'">Kosova (RS)</xsl:when>
            <xsl:when test="$CountryType='CR'">Kosta Rika</xsl:when>
            <xsl:when test="$CountryType='CU'">Küba</xsl:when>
            <xsl:when test="$CountryType='KW'">Kuveyt</xsl:when>
            <xsl:when test="$CountryType='KP'">Kuzey Kore</xsl:when>
            <xsl:when test="$CountryType='LA'">Laos</xsl:when>
            <xsl:when test="$CountryType='LS'">Lesoto</xsl:when>
            <xsl:when test="$CountryType='LV'">Letonya</xsl:when>
            <xsl:when test="$CountryType='LR'">Liberya</xsl:when>
            <xsl:when test="$CountryType='LY'">Libya</xsl:when>
            <xsl:when test="$CountryType='LI'">Lihtenştayn</xsl:when>
            <xsl:when test="$CountryType='LT'">Litvanya</xsl:when>
            <xsl:when test="$CountryType='LB'">Lübnan</xsl:when>
            <xsl:when test="$CountryType='LU'">Lüksemburg</xsl:when>
            <xsl:when test="$CountryType='HU'">Macaristan</xsl:when>
            <xsl:when test="$CountryType='MG'">Madagaskar</xsl:when>
            <xsl:when test="$CountryType='MO'">Makao (CN)</xsl:when>
            <xsl:when test="$CountryType='MK'">Makedonya</xsl:when>
            <xsl:when test="$CountryType='MW'">Malavi</xsl:when>
            <xsl:when test="$CountryType='MV'">Maldivler</xsl:when>
            <xsl:when test="$CountryType='MY'">Malezya</xsl:when>
            <xsl:when test="$CountryType='ML'">Mali</xsl:when>
            <xsl:when test="$CountryType='MT'">Malta</xsl:when>
            <xsl:when test="$CountryType='IM'">Man Adası (GB)</xsl:when>
            <xsl:when test="$CountryType='MH'">Marshall Adaları</xsl:when>
            <xsl:when test="$CountryType='MQ'">Martinique (FR)</xsl:when>
            <xsl:when test="$CountryType='MU'">Mauritius</xsl:when>
            <xsl:when test="$CountryType='YT'">Mayotte (FR)</xsl:when>
            <xsl:when test="$CountryType='MX'">Meksika</xsl:when>
            <xsl:when test="$CountryType='FM'">Mikronezya</xsl:when>
            <xsl:when test="$CountryType='EG'">Mısır</xsl:when>
            <xsl:when test="$CountryType='MN'">Moğolistan</xsl:when>
            <xsl:when test="$CountryType='MD'">Moldova</xsl:when>
            <xsl:when test="$CountryType='MC'">Monako</xsl:when>
            <xsl:when test="$CountryType='MR'">Moritanya</xsl:when>
            <xsl:when test="$CountryType='MZ'">Mozambik</xsl:when>
            <xsl:when test="$CountryType='MM'">Myanmar</xsl:when>
            <xsl:when test="$CountryType='NA'">Namibya</xsl:when>
            <xsl:when test="$CountryType='NR'">Nauru</xsl:when>
            <xsl:when test="$CountryType='NP'">Nepal</xsl:when>
            <xsl:when test="$CountryType='NE'">Nijer</xsl:when>
            <xsl:when test="$CountryType='NG'">Nijerya</xsl:when>
            <xsl:when test="$CountryType='NI'">Nikaragua</xsl:when>
            <xsl:when test="$CountryType='NO'">Norveç</xsl:when>
            <xsl:when test="$CountryType='CF'">Orta Afrika Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='UZ'">Özbekistan</xsl:when>
            <xsl:when test="$CountryType='PK'">Pakistan</xsl:when>
            <xsl:when test="$CountryType='PW'">Palau</xsl:when>
            <xsl:when test="$CountryType='PA'">Panama</xsl:when>
            <xsl:when test="$CountryType='PG'">Papua Yeni Gine</xsl:when>
            <xsl:when test="$CountryType='PY'">Paraguay</xsl:when>
            <xsl:when test="$CountryType='PE'">Peru</xsl:when>
            <xsl:when test="$CountryType='PL'">Polonya</xsl:when>
            <xsl:when test="$CountryType='PT'">Portekiz</xsl:when>
            <xsl:when test="$CountryType='PR'">Porto Riko (US)</xsl:when>
            <xsl:when test="$CountryType='RE'">Réunion (FR)</xsl:when>
            <xsl:when test="$CountryType='RO'">Romanya</xsl:when>
            <xsl:when test="$CountryType='RW'">Ruanda</xsl:when>
            <xsl:when test="$CountryType='RU'">Rusya</xsl:when>
            <xsl:when test="$CountryType='BL'">Saint Barthélemy (FR)</xsl:when>
            <xsl:when test="$CountryType='KN'">Saint Kitts ve Nevis</xsl:when>
            <xsl:when test="$CountryType='LC'">Saint Lucia</xsl:when>
            <xsl:when test="$CountryType='PM'">Saint Pierre ve Miquelon (FR)</xsl:when>
            <xsl:when test="$CountryType='VC'">Saint Vincent ve Grenadinler</xsl:when>
            <xsl:when test="$CountryType='WS'">Samoa</xsl:when>
            <xsl:when test="$CountryType='SM'">San Marino</xsl:when>
            <xsl:when test="$CountryType='ST'">São Tomé ve Príncipe</xsl:when>
            <xsl:when test="$CountryType='SN'">Senegal</xsl:when>
            <xsl:when test="$CountryType='SC'">Seyşeller</xsl:when>
            <xsl:when test="$CountryType='SL'">Sierra Leone</xsl:when>
            <xsl:when test="$CountryType='CL'">Şili</xsl:when>
            <xsl:when test="$CountryType='SG'">Singapur</xsl:when>
            <xsl:when test="$CountryType='RS'">Sırbistan</xsl:when>
            <xsl:when test="$CountryType='SK'">Slovakya Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='SI'">Slovenya</xsl:when>
            <xsl:when test="$CountryType='SB'">Solomon Adaları</xsl:when>
            <xsl:when test="$CountryType='SO'">Somali</xsl:when>
            <xsl:when test="$CountryType='SS'">South Sudan</xsl:when>
            <xsl:when test="$CountryType='SJ'">Spitsbergen (NO)</xsl:when>
            <xsl:when test="$CountryType='LK'">Sri Lanka</xsl:when>
            <xsl:when test="$CountryType='SD'">Sudan</xsl:when>
            <xsl:when test="$CountryType='SR'">Surinam</xsl:when>
            <xsl:when test="$CountryType='SY'">Suriye</xsl:when>
            <xsl:when test="$CountryType='SA'">Suudi Arabistan</xsl:when>
            <xsl:when test="$CountryType='SZ'">Svaziland</xsl:when>
            <xsl:when test="$CountryType='TJ'">Tacikistan</xsl:when>
            <xsl:when test="$CountryType='TZ'">Tanzanya</xsl:when>
            <xsl:when test="$CountryType='TH'">Tayland</xsl:when>
            <xsl:when test="$CountryType='TW'">Tayvan</xsl:when>
            <xsl:when test="$CountryType='TG'">Togo</xsl:when>
            <xsl:when test="$CountryType='TO'">Tonga</xsl:when>
            <xsl:when test="$CountryType='TT'">Trinidad ve Tobago</xsl:when>
            <xsl:when test="$CountryType='TN'">Tunus</xsl:when>
            <xsl:when test="$CountryType='TR'">Türkiye</xsl:when>
            <xsl:when test="$CountryType='TM'">Türkmenistan</xsl:when>
            <xsl:when test="$CountryType='TC'">Turks ve Caicos</xsl:when>
            <xsl:when test="$CountryType='TV'">Tuvalu</xsl:when>
            <xsl:when test="$CountryType='UG'">Uganda</xsl:when>
            <xsl:when test="$CountryType='UA'">Ukrayna</xsl:when>
            <xsl:when test="$CountryType='OM'">Umman</xsl:when>
            <xsl:when test="$CountryType='JO'">Ürdün</xsl:when>
            <xsl:when test="$CountryType='UY'">Uruguay</xsl:when>
            <xsl:when test="$CountryType='VU'">Vanuatu</xsl:when>
            <xsl:when test="$CountryType='VA'">Vatikan</xsl:when>
            <xsl:when test="$CountryType='VE'">Venezuela</xsl:when>
            <xsl:when test="$CountryType='VN'">Vietnam</xsl:when>
            <xsl:when test="$CountryType='WF'">Wallis ve Futuna (FR)</xsl:when>
            <xsl:when test="$CountryType='YE'">Yemen</xsl:when>
            <xsl:when test="$CountryType='NC'">Yeni Kaledonya (FR)</xsl:when>
            <xsl:when test="$CountryType='NZ'">Yeni Zelanda</xsl:when>
            <xsl:when test="$CountryType='CV'">Yeşil Burun Adaları</xsl:when>
            <xsl:when test="$CountryType='GR'">Yunanistan</xsl:when>
            <xsl:when test="$CountryType='ZM'">Zambiya</xsl:when>
            <xsl:when test="$CountryType='ZW'">Zimbabve</xsl:when>
            <xsl:otherwise><xsl:value-of select="$CountryType"/></xsl:otherwise>
        </xsl:choose>
        
    </xsl:template>
    <xsl:template name='Party_Other'>
        <xsl:param name="PartyType" />
        <xsl:for-each select="cbc:WebsiteURI">
            <tr align="left">
                <td>
                    <xsl:text>Web Sitesi: </xsl:text>
                    <xsl:value-of select="."/>
                </td>
            </tr>
        </xsl:for-each>
        <xsl:for-each select="cac:Contact/cbc:ElectronicMail">
            <tr align="left">
                <td>
                    <xsl:text>E-Posta: </xsl:text>
                    <xsl:value-of select="."/>
                </td>
            </tr>
        </xsl:for-each> 
        <xsl:for-each select="cac:Contact">
            <xsl:if test="cbc:Telephone or cbc:Telefax">
                <tr align="left">
                    <td style="width:469px; " align="left">
                        <xsl:for-each select="cbc:Telephone">
                            <xsl:text>Tel: </xsl:text>
                            <xsl:apply-templates/>
                        </xsl:for-each>
                        <xsl:for-each select="cbc:Telefax">
                            <xsl:text> Fax: </xsl:text>
                            <xsl:apply-templates/>
                        </xsl:for-each>
                        <xsl:text>&#160;</xsl:text>
                    </td>
                </tr>
            </xsl:if>
        </xsl:for-each>
        <xsl:if test="$PartyType!='TAXFREE' and $PartyType!='EXPORT'">
            <xsl:for-each select="cac:PartyTaxScheme/cac:TaxScheme/cbc:Name">
                <tr align="left">
                    <td>
                        <xsl:text>Vergi Dairesi: </xsl:text>
                        <xsl:apply-templates/>
                    </td>
                </tr>
            </xsl:for-each>
            <xsl:for-each select="cac:PartyIdentification">
            <tr align="left">
                <td>
                    <xsl:value-of select="cbc:ID/@schemeID"/>
                    <xsl:text>: </xsl:text>
                    <xsl:value-of select="cbc:ID"/>
                    
                </td>
            </tr>
            </xsl:for-each>
        </xsl:if>
    </xsl:template>
      <xsl:template name='Party_Otherx'>
    <xsl:param name="PartyTypex" />
   <tr align="left">
          <td>
          
                   
     <xsl:for-each select="cac:DeliveryAddress">
          
                    <xsl:value-of disable-output-escaping="yes" select="//n1:Invoice/cac:Delivery/cac:DeliveryAddress/cbc:Name" />  
                    <br/>
            <xsl:value-of disable-output-escaping="yes" select="cbc:StreetName"/> 
<br/>           
            <xsl:value-of disable-output-escaping="yes" select="cbc:CitySubdivisionName"/>
            <xsl:text> / </xsl:text>
            <xsl:value-of disable-output-escaping="yes" select="cbc:CityName"/>
             <br/>
            <xsl:value-of disable-output-escaping="yes" select="//n1:Invoice/cac:Delivery/cbc:ID" />
         
      </xsl:for-each>
     </td>
        </tr>

  </xsl:template> 
    <xsl:template name="Curr_Type">
        <xsl:value-of select="format-number(., '###.##0,00', 'european')"/>     
        <xsl:if test="@currencyID">
            <xsl:text> </xsl:text>
            <xsl:choose>
                <xsl:when test="@currencyID = 'TRL' or @currencyID = 'TRY'">
                    <xsl:text>TL</xsl:text>                 
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="@currencyID"/>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:if>       
    </xsl:template>
  <xsl:template name="Curr_Typee">
    <xsl:param name="format"/>
    <xsl:param name="valuePath"/>
    <xsl:value-of select="format-number($valuePath, $format, 'european')"/>
    <xsl:if test="$valuePath/@currencyID">
      <xsl:text> </xsl:text>
      <xsl:choose>
        <xsl:when test="$valuePath/@currencyID = 'TRL' or $valuePath/@currencyID = 'TRY'">
          <xsl:text>TL</xsl:text>
        </xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="$valuePath/@currencyID"/>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:if>
  </xsl:template>
</xsl:stylesheet>