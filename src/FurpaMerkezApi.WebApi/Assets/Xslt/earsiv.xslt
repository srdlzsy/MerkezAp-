<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                xmlns:ccts="urn:un:unece:uncefact:documentation:2" xmlns:clm54217="urn:un:unece:uncefact:codelist:specification:54217:2001" xmlns:clm5639="urn:un:unece:uncefact:codelist:specification:5639:1988"
                xmlns:clm66411="urn:un:unece:uncefact:codelist:specification:66411:2001" xmlns:clmIANAMIMEMediaType="urn:un:unece:uncefact:codelist:specification:IANAMIMEMediaType:2003" xmlns:fn="http://www.w3.org/2005/xpath-functions"
                xmlns:link="http://www.xbrl.org/2003/linkbase" xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2" xmlns:qdt="urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2"
                xmlns:udt="urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2" xmlns:xbrldi="http://xbrl.org/2006/xbrldi" xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:xdt="http://www.w3.org/2005/xpath-datatypes"
                xmlns:xlink="http://www.w3.org/1999/xlink" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                exclude-result-prefixes="cac cbc ccts clm54217 clm5639 clm66411 clmIANAMIMEMediaType fn link n1 qdt udt xbrldi xbrli xdt xlink xs xsd xsi">
	<xsl:decimal-format name="european" decimal-separator="," grouping-separator="." NaN=""/>
	<xsl:output version="4.0" method="html" indent="no" encoding="UTF-8" doctype-public="-//W3C//DTD HTML 4.01 Transitional//EN" doctype-system="http://www.w3.org/TR/html4/loose.dtd"/>
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
			<body style="margin-left=0.6in; margin-right=0.6in; margin-top=0.79in; margin-bottom=0.79in">
				<xsl:for-each select="$XML">
					<table style="border-color:blue; " border="0" cellspacing="0px" width="800" cellpadding="0px">
						<tbody>
							<tr valign="top">
								<td width="40%">
									<br/>
									<table align="center" border="0" width="100%">
										<tbody>
											<hr/>
											<tr align="left">
												<xsl:for-each select="n1:Invoice">
													<xsl:for-each select="cac:AccountingSupplierParty">
														<xsl:for-each select="cac:Party">
															<td align="left">
																<xsl:if test="cac:PartyName">
																	<xsl:value-of select="cac:PartyName/cbc:Name"/>
																	<br/>
																</xsl:if>
																<xsl:for-each select="cac:Person">
																	<xsl:for-each select="cbc:Title">
																		<xsl:apply-templates/>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																	<xsl:for-each select="cbc:FirstName">
																		<xsl:apply-templates/>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																	<xsl:for-each select="cbc:MiddleName">
																		<xsl:apply-templates/>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																	<xsl:for-each select="cbc:FamilyName">
																		<xsl:apply-templates/>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																	<xsl:for-each select="cbc:NameSuffix">
																		<xsl:apply-templates/>
																	</xsl:for-each>
																</xsl:for-each>
															</td>
														</xsl:for-each>
													</xsl:for-each>
												</xsl:for-each>
											</tr>
											<tr align="left">
												<xsl:for-each select="n1:Invoice">
													<xsl:for-each select="cac:AccountingSupplierParty">
														<xsl:for-each select="cac:Party">
															<td align="left">
																<xsl:for-each select="cac:PostalAddress">
																	<xsl:for-each select="cbc:StreetName">
																		<xsl:apply-templates/>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																	<xsl:for-each select="cbc:BuildingName">
																		<xsl:apply-templates/>
																	</xsl:for-each>
																	<xsl:if test="cbc:BuildingNumber">
																		<span>
																			<xsl:text> No:</xsl:text>
																		</span>
																		<xsl:for-each select="cbc:BuildingNumber">
																			<xsl:apply-templates/>
																		</xsl:for-each>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:if>
																	<br/>
																	<xsl:for-each select="cbc:PostalZone">
																		<xsl:apply-templates/>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																	<xsl:for-each select="cbc:CitySubdivisionName">
																		<xsl:apply-templates/>
																	</xsl:for-each>
																	<span>
																		<xsl:text>/ </xsl:text>
																	</span>
																	<xsl:for-each select="cbc:CityName">
																		<xsl:apply-templates/>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																</xsl:for-each>
															</td>
														</xsl:for-each>
													</xsl:for-each>
												</xsl:for-each>
											</tr>
											<xsl:if test="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:Telephone or //n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:Telefax">
												<tr align="left">
													<xsl:for-each select="n1:Invoice">
														<xsl:for-each select="cac:AccountingSupplierParty">
															<xsl:for-each select="cac:Party">
																<td align="left">
																	<xsl:for-each select="cac:Contact">
																		<xsl:if test="cbc:Telephone">
																			<span>
																				<xsl:text>Tel: </xsl:text>
																			</span>
																			<xsl:for-each select="cbc:Telephone">
																				<xsl:apply-templates/>
																			</xsl:for-each>
																		</xsl:if>
																		<xsl:if test="cbc:Telefax">
																			<span>
																				<xsl:text> Fax: </xsl:text>
																			</span>
																			<xsl:for-each select="cbc:Telefax">
																				<xsl:apply-templates/>
																			</xsl:for-each>
																		</xsl:if>
																		<span>
																			<xsl:text>&#xA0;</xsl:text>
																		</span>
																	</xsl:for-each>
																</td>
															</xsl:for-each>
														</xsl:for-each>
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
												<xsl:for-each select="n1:Invoice">
													<xsl:for-each select="cac:AccountingSupplierParty">
														<xsl:for-each select="cac:Party">
															<td align="left">
																<span>
																	<xsl:text>Vergi Dairesi: </xsl:text>
																</span>
																<xsl:for-each select="cac:PartyTaxScheme">
																	<xsl:for-each select="cac:TaxScheme">
																		<xsl:for-each select="cbc:Name">
																			<xsl:apply-templates/>
																		</xsl:for-each>
																	</xsl:for-each>
																	<span>
																		<xsl:text>&#xA0; </xsl:text>
																	</span>
																</xsl:for-each>
															</td>
														</xsl:for-each>
													</xsl:for-each>
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
									    src="data:image/jpeg;base64,/9j/4QAYRXhpZgAASUkqAAgAAAAAAAAAAAAAAP/sABFEdWNreQABAAQAAABkAAD/4QMZaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wLwA8P3hwYWNrZXQgYmVnaW49Iu+7vyIgaWQ9Ilc1TTBNcENlaGlIenJlU3pOVGN6a2M5ZCI/PiA8eDp4bXBtZXRhIHhtbG5zOng9ImFkb2JlOm5zOm1ldGEvIiB4OnhtcHRrPSJBZG9iZSBYTVAgQ29yZSA1LjYtYzEzMiA3OS4xNTkyODQsIDIwMTYvMDQvMTktMTM6MTM6NDAgICAgICAgICI+IDxyZGY6UkRGIHhtbG5zOnJkZj0iaHR0cDovL3d3dy53My5vcmcvMTk5OS8wMi8yMi1yZGYtc3ludGF4LW5zIyI+IDxyZGY6RGVzY3JpcHRpb24gcmRmOmFib3V0PSIiIHhtbG5zOnhtcE1NPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvbW0vIiB4bWxuczpzdFJlZj0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL3NUeXBlL1Jlc291cmNlUmVmIyIgeG1sbnM6eG1wPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvIiB4bXBNTTpEb2N1bWVudElEPSJ4bXAuZGlkOjZDNDJBNEI2QjVCRDExRThCQjM0REIwQkZGMEQxODY0IiB4bXBNTTpJbnN0YW5jZUlEPSJ4bXAuaWlkOjZDNDJBNEI1QjVCRDExRThCQjM0REIwQkZGMEQxODY0IiB4bXA6Q3JlYXRvclRvb2w9IkFkb2JlIFBob3Rvc2hvcCBDUzQgV2luZG93cyI+IDx4bXBNTTpEZXJpdmVkRnJvbSBzdFJlZjppbnN0YW5jZUlEPSIzREVENkU1N0FDREVDNEJBNzkxNUM2M0NCN0RENzM0NyIgc3RSZWY6ZG9jdW1lbnRJRD0iM0RFRDZFNTdBQ0RFQzRCQTc5MTVDNjNDQjdERDczNDciLz4gPC9yZGY6RGVzY3JpcHRpb24+IDwvcmRmOlJERj4gPC94OnhtcG1ldGE+IDw/eHBhY2tldCBlbmQ9InIiPz7/7gAOQWRvYmUAZMAAAAAB/9sAhAABAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEB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"/>

									<h1 align="center">
										<span style="font-weight:bold; ">
											<xsl:text>e-Arşiv Fatura</xsl:text>
										</span>
									</h1>
								</td>
								<td width="40%" align="right" valign="middle"><img style='max-width:300px;' align='right' alt='' src='data:image/jpeg;base64,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rfP/AFzX/CrSWwUcAA+wqais4YHDwd4QS9Ehucnuxnl4XB5/DrQFOOlPorp5VsSlYKKKKEgA9KQilooaAjEW0Hv+FMe1WQfMinPXip6KyqUIVPjSY02tjPPh6zY/8eVt/wB+1/wqzFZJCuFRUAGBgcCp6KyjgMNF3jTSfohucno2RrGQv/1qXBOeKfRXTyokKKKKoApgBUnvmn0UmgRHsyOnFNa3DjlQfqKmorOdGE1aauNO2xRm0C0nOXtbdj6mJSakg02K3UCOJIwOwUCrVFYLL8MnzKmr+hTqSas2ReThs4/SnqmadRXUopbGdhAOfrS0UU0hh1pCue+M0tFMCPYfQih4Q45UH61JRUSpxkrS19QWmxUm0W1uTmS1hkPqyA0QaRb2u7yreGLd12oBmrdFcv8AZ2FvdU439EVzyta5EIMYxwPpTmiB4OafRXWoJLlSItrcRV20tFFUMCMik2/jS0UrdQGFCDmmvEWGCFP4VLRScE1aWoLQqz6TBdY82CKTByNyg4pINGtbY5jtoUPqqAVborl/s/DX5vZq/oiueVrXI1gCnhQPpxS7DnpT6K6Y04xVoqxL13GjPpinUUVSQBRRRTAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigD//2Q=='/></td>
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
																<xsl:for-each select="n1:Invoice">
																	<xsl:for-each select="cac:AccountingCustomerParty">
																		<xsl:for-each select="cac:Party">
																			<td style="width:469px; " align="left">
																				<span style="font-weight:bold; ">
																					<xsl:text>SAYIN</xsl:text>
																				</span>
																			</td>
																		</xsl:for-each>
																	</xsl:for-each>
																</xsl:for-each>
															</tr>
															<tr>
																<xsl:for-each select="n1:Invoice">
																	<xsl:for-each select="cac:AccountingCustomerParty">
																		<xsl:for-each select="cac:Party">
																			<td style="width:469px; " align="left">
																				<xsl:if test="cac:PartyName">
																					<xsl:value-of select="cac:PartyName/cbc:Name"/>
																					<br/>
																				</xsl:if>
																				<xsl:for-each select="cac:Person">
																					<xsl:for-each select="cbc:Title">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:FirstName">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:MiddleName">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0; </xsl:text>
																						</span>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:FamilyName">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:NameSuffix">
																						<xsl:apply-templates/>
																					</xsl:for-each>
																				</xsl:for-each>
																			</td>
																		</xsl:for-each>
																	</xsl:for-each>
																</xsl:for-each>
															</tr>
															<tr>
																<xsl:for-each select="n1:Invoice">
																	<xsl:for-each select="cac:AccountingCustomerParty">
																		<xsl:for-each select="cac:Party">
																			<td style="width:469px; " align="left">
																				<xsl:for-each select="cac:PostalAddress">
																					<xsl:for-each select="cbc:StreetName">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:BuildingName">
																						<xsl:apply-templates/>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:BuildingNumber">
																						<span>
																							<xsl:text> No:</xsl:text>
																						</span>
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																					<br/>
																					<xsl:for-each select="cbc:Room">
																						<span>
																							<xsl:text>Kapı No:</xsl:text>
																						</span>
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																					<br/>
																					<xsl:for-each select="cbc:PostalZone">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:CitySubdivisionName">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>/ </xsl:text>
																						</span>
																					</xsl:for-each>
																					<xsl:for-each select="cbc:CityName">
																						<xsl:apply-templates/>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</xsl:for-each>
																				</xsl:for-each>
																			</td>
																		</xsl:for-each>
																	</xsl:for-each>
																</xsl:for-each>
															</tr>
															<xsl:for-each select="cbc:WebsiteURI">
																<xsl:if test=". !=''">
																	<tr align="left">
																		<td>
																			<xsl:text>Web Sitesi: </xsl:text>
																			<xsl:value-of select="."/>
																		</td>
																	</tr>
																</xsl:if>
															</xsl:for-each>
															<xsl:for-each select="cac:Contact/cbc:ElectronicMail">
																<xsl:if test=". !=''">
																	<tr align="left">
																		<td>
																			<xsl:text>E-Posta: </xsl:text>
																			<xsl:value-of select="."/>
																		</td>
																	</tr>
																</xsl:if>
															</xsl:for-each>
															<xsl:for-each select="n1:Invoice">
																<xsl:for-each select="cac:AccountingCustomerParty">
																	<xsl:for-each select="cac:Party">
																		<xsl:for-each select="cac:Contact">
																			<xsl:if test="cbc:Telephone or cbc:Telefax">
																				<tr align="left">
																					<td style="width:469px; " align="left">
																						<xsl:for-each select="cbc:Telephone">
																							<span>
																								<xsl:text>Tel: </xsl:text>
																							</span>
																							<xsl:apply-templates/>
																						</xsl:for-each>
																						<xsl:for-each select="cbc:Telefax">
																							<span>
																								<xsl:text> Fax: </xsl:text>
																							</span>
																							<xsl:apply-templates/>
																						</xsl:for-each>
																						<span>
																							<xsl:text>&#xA0;</xsl:text>
																						</span>
																					</td>
																				</tr>
																			</xsl:if>
																		</xsl:for-each>
																	</xsl:for-each>
																</xsl:for-each>
															</xsl:for-each>
															<xsl:if test="//n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyTaxScheme/cac:TaxScheme/cbc:Name">
																<tr align="left">
																	<td>
																		<span>
																			<xsl:text>Vergi Dairesi: </xsl:text>
																			<xsl:value-of select="//n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyTaxScheme/cac:TaxScheme/cbc:Name"/>
																		</span>
																	</td>
																</tr>
															</xsl:if>
															<xsl:for-each select="//n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification">
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
											</tr>
										</tbody>
									</table>
									<br/>
								</td>
								<td width="20%" align="center" valign="top"><img style='max-width:200px;' alt='İmza' src='data:image/png;base64,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'/></td>
								<td width="40%" align="center" valign="bottom">
									<table border="1" height="13" id="despatchTable">
										<tbody>
											<tr>
												<td style="width:105px;" align="left">
													<span style="font-weight:bold; ">
														<xsl:text>Özelleştirme No</xsl:text>
													</span>
												</td>
												<td style="width:110px;" align="left">
													<xsl:for-each select="n1:Invoice">
														<xsl:for-each select="cbc:CustomizationID">
															<xsl:apply-templates/>
														</xsl:for-each>
													</xsl:for-each>
												</td>
											</tr>
											<tr style="height:13px; ">
												<td align="left">
													<span style="font-weight:bold; ">
														<xsl:text>Senaryo</xsl:text>
													</span>
												</td>
												<td align="left">
													<xsl:for-each select="n1:Invoice">
														<xsl:for-each select="cbc:ProfileID">
															<xsl:apply-templates/>
														</xsl:for-each>
													</xsl:for-each>
												</td>
											</tr>
											<tr style="height:13px; ">
												<td align="left">
													<span style="font-weight:bold; ">
														<xsl:text>Fatura Tipi</xsl:text>
													</span>
												</td>
												<td align="left">
													<xsl:for-each select="n1:Invoice">
														<xsl:for-each select="cbc:InvoiceTypeCode">
															<xsl:apply-templates/>
														</xsl:for-each>
													</xsl:for-each>
												</td>
											</tr>
											<tr style="height:13px; ">
												<td align="left">
													<span style="font-weight:bold; ">
														<xsl:text>Fatura No</xsl:text>
													</span>
												</td>
												<td align="left">
													<xsl:for-each select="n1:Invoice">
														<xsl:for-each select="cbc:ID">
															<xsl:apply-templates/>
														</xsl:for-each>
													</xsl:for-each>
												</td>
											</tr>
											<tr style="height:13px; ">
												<td align="left">
													<span style="font-weight:bold; ">
														<xsl:text>Fatura Tarihi</xsl:text>
													</span>
												</td>
												<td align="left">
													<xsl:for-each select="n1:Invoice">
														<xsl:for-each select="cbc:IssueDate">
															<xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
														</xsl:for-each>
													</xsl:for-each>
												</td>
											</tr>
											<xsl:if test="n1:Invoice/cbc:IssueDate !=''">
												<tr style="height:13px; ">
													<td align="left">
														<span style="font-weight:bold; ">
															<xsl:text>Düzenleme Tarihi</xsl:text>
														</span>
													</td>
													<td align="left">
														<xsl:for-each select="n1:Invoice">
															<xsl:for-each select="cbc:IssueDate">
																<xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
															</xsl:for-each>
														</xsl:for-each>
													</td>
												</tr>
											</xsl:if>
											<xsl:if test="n1:Invoice/cbc:IssueTime !=''">
												<tr style="height:13px; ">
													<td align="left">
														<span style="font-weight:bold; ">
															<xsl:text>Düzenleme Saati</xsl:text>
														</span>
													</td>
													<td align="left">
														<xsl:for-each select="n1:Invoice">
															<xsl:value-of select="substring(cbc:IssueTime,1,8)"/>
														</xsl:for-each>
													</td>
												</tr>
											</xsl:if>
											<xsl:if test="n1:Invoice/cac:DespatchDocumentReference">
												<xsl:if test="count(n1:Invoice/cac:DespatchDocumentReference) = 1">
													<tr style="height:13px; ">
														<td align="left">
															<span style="font-weight:bold; ">
																<xsl:text>İrsaliye No</xsl:text>
															</span>
															<span>
																<xsl:text>&#xA0;</xsl:text>
															</span>
														</td>
														<td align="left">
															<xsl:value-of select="n1:Invoice/cac:DespatchDocumentReference/cbc:ID"/>
														</td>
													</tr>
													<tr style="height:13px; ">
														<td align="left">
															<span style="font-weight:bold; ">
																<xsl:text>İrsaliye Tarihi</xsl:text>
															</span>
														</td>
														<td align="left">
															<xsl:for-each select="n1:Invoice/cac:DespatchDocumentReference/cbc:IssueDate">
																<xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
															</xsl:for-each>
														</td>
													</tr>
												</xsl:if>
											</xsl:if>
											<xsl:if test="//n1:Invoice/cac:OrderReference">
												<xsl:if test="count(n1:Invoice/cac:OrderReference) = 1">
													<tr style="height:13px">
														<td align="left">
															<span style="font-weight:bold; ">
																<xsl:text>Sipariş No</xsl:text>
															</span>
														</td>
														<td align="left">
															<xsl:value-of select="n1:Invoice/cac:OrderReference/cbc:ID"/>
														</td>
													</tr>
												</xsl:if>
											</xsl:if>
											<xsl:if test="//n1:Invoice/cac:OrderReference/cbc:IssueDate">
												<xsl:if test="count(n1:Invoice/cac:OrderReference/cbc:IssueDate) = 1">
													<tr style="height:13px">
														<td align="left">
															<span style="font-weight:bold; ">
																<xsl:text>Sipariş Tarihi</xsl:text>
															</span>
														</td>
														<td align="left">
															<xsl:for-each select="n1:Invoice/cac:OrderReference/cbc:IssueDate">
																<xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
															</xsl:for-each>
														</td>
													</tr>
												</xsl:if>
											</xsl:if>
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
											<xsl:for-each select="n1:Invoice">
												<xsl:for-each select="cbc:UUID">
													<xsl:apply-templates/>
												</xsl:for-each>
											</xsl:for-each>
										</td>
									</tr>
								</table>
							</tr>
						</tbody>
					</table>
					<div id="lineTableAligner">
						<span>
							<xsl:text>&#xA0;</xsl:text>
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
								<td id="lineTableTd" style="width:15%" align="center">
									<span style="font-weight:bold; ">
										<xsl:text>Mal Hizmet</xsl:text>
									</span>
								</td>
								<td id="lineTableTd" style="width:10.4%" align="center">
									<span style="font-weight:bold;">
										<xsl:text>Miktar</xsl:text>
									</span>
								</td>
								<td id="lineTableTd" style="width:14%" align="center">
									<span style="font-weight:bold; ">
										<xsl:text>Birim Fiyat</xsl:text>
									</span>
								</td>
								<td id="lineTableTd" style="width:11%" align="center">
									<span style="font-weight:bold; ">
										<xsl:text>İskonto Oranı(%)</xsl:text>
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
								<td id="lineTableTd" style="width:10%; " align="center">
									<span style="font-weight:bold; ">
										<xsl:text>Diğer Vergiler</xsl:text>
									</span>
								</td>
								<td id="lineTableTd" style="width:10.6%" align="center">
									<span style="font-weight:bold; ">
										<xsl:text>Mal Hizmet Tutarı</xsl:text>
									</span>
								</td>
							</tr>
							<xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
								<xsl:choose>
									<xsl:when test=".">
										<xsl:apply-templates select="."/>
									</xsl:when>
									<xsl:otherwise>
										<xsl:apply-templates select="//n1:Invoice"/>
									</xsl:otherwise>
								</xsl:choose>
							</xsl:for-each>
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
							<span>
								<xsl:value-of select="format-number(sum(n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount) + sum(n1:Invoice/cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount), '###.##0,00', 'european')"/>
								<xsl:text> </xsl:text>
								<xsl:choose>
									<xsl:when test="n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount/@currencyID = 'TRY' or n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount/@currencyID = 'TRL'">
										<xsl:text>TL</xsl:text>
									</xsl:when>
									<xsl:otherwise>
										<xsl:value-of select="n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount/@currencyID"/>
									</xsl:otherwise>
								</xsl:choose>
							</span>
						</td>
					</tr>
					<tr id="budgetContainerTr" align="right">
						<td id="budgetContainerDummyTd"/>
						<td id="lineTableBudgetTd" align="right" width="200px">
							<span style="font-weight:bold; ">
								<xsl:text>Toplam İskonto</xsl:text>
							</span>
						</td>
						<td id="lineTableBudgetTd" style="width:81px; " align="right">
							<span>
								<xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount">
									<xsl:call-template name="Curr_Type">
										<xsl:with-param name="valuePath" select="."/>
										<xsl:with-param name="format" select="'###.##0,00'"/>
									</xsl:call-template>
								</xsl:for-each>
							</span>
						</td>
					</tr>
					<xsl:if test="number(n1:Invoice/cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount) &gt; 0">
						<tr id="budgetContainerTr" align="right">
							<td id="budgetContainerDummyTd"/>
							<td id="lineTableBudgetTd" align="right" width="200px">
								<span style="font-weight:bold; ">
									<xsl:text>&#x130;skonto Sonras&#x131; Vergi Hari&#xE7; Tutar</xsl:text>
								</span>
							</td>
							<td id="lineTableBudgetTd" style="width:81px; " align="right">
								<span>
									<xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxExclusiveAmount">
										<xsl:call-template name="Curr_Type">
											<xsl:with-param name="valuePath" select="."/>
											<xsl:with-param name="format" select="'###.##0,00'"/>
										</xsl:call-template>
									</xsl:for-each>
								</span>
							</td>
						</tr>
					</xsl:if>
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
									<xsl:call-template name="Curr_Type">
										<xsl:with-param name="valuePath" select="../../cbc:TaxAmount"/>
										<xsl:with-param name="format" select="'###.##0,00'"/>
									</xsl:call-template>
								</xsl:for-each>
							</td>
						</tr>
					</xsl:for-each>
					<xsl:for-each select="n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal">
						<xsl:if test="cbc:TaxAmount != ''">
							<tr id="budgetContainerTr" align="right">
								<td id="budgetContainerDummyTd"/>
								<td id="lineTableBudgetTd" width="211px" align="right">
									<span style="font-weight:bold; ">
										<xsl:text>KDV Tevkifat-[</xsl:text>
										<xsl:value-of select="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode"/>
										<xsl:text>]-</xsl:text>
										<xsl:text>(%</xsl:text>
										<xsl:value-of select="cbc:Percent"/>
										<xsl:text>)</xsl:text>
									</span>
								</td>
								<td id="lineTableBudgetTd" style="width:82px; " align="right">
									<xsl:for-each select="cac:TaxCategory/cac:TaxScheme">
										<xsl:text> </xsl:text>
										<xsl:call-template name="Curr_Type">
											<xsl:with-param name="valuePath" select="../../cbc:TaxAmount"/>
											<xsl:with-param name="format" select="'###.##0,00'"/>
										</xsl:call-template>
									</xsl:for-each>
								</td>
							</tr>
						</xsl:if>
					</xsl:for-each>
					<tr id="budgetContainerTr" align="right">
						<td id="budgetContainerDummyTd"/>
						<td id="lineTableBudgetTd" width="200px" align="right">
							<span style="font-weight:bold; ">
								<xsl:text>Vergiler Dahil Toplam Tutar</xsl:text>
							</span>
						</td>
						<td id="lineTableBudgetTd" style="width:82px; " align="right">
							<xsl:for-each select="n1:Invoice">
								<xsl:for-each select="cac:LegalMonetaryTotal">
									<xsl:for-each select="cbc:TaxInclusiveAmount">
										<xsl:call-template name="Curr_Type">
											<xsl:with-param name="valuePath" select="."/>
											<xsl:with-param name="format" select="'###.##0,00'"/>
										</xsl:call-template>
									</xsl:for-each>
								</xsl:for-each>
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
								<xsl:call-template name="Curr_Type">
									<xsl:with-param name="valuePath" select="."/>
									<xsl:with-param name="format" select="'###.##0,00'"/>
								</xsl:call-template>
							</xsl:for-each>
						</td>
					</tr>
					<xsl:if test="//n1:Invoice/cbc:DocumentCurrencyCode != 'TRY'">
						<tr id="budgetContainerTr" align="right">
							<td id="budgetContainerDummyTd"/>
							<td id="lineTableBudgetTd" align="right" width="200px">
								<span style="font-weight:bold; ">
									<xsl:text>Toplam İskonto (TL)</xsl:text>
								</span>
							</td>
							<td id="lineTableBudgetTd" style="width:81px; " align="right">
								<span>
									<xsl:value-of select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
									<xsl:text> TL</xsl:text>
								</span>
							</td>
						</tr>
					</xsl:if>
					<xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
						<xsl:if test="//n1:Invoice/cbc:DocumentCurrencyCode != 'TRY'">
							<tr align="right">
								<td/>
								<td id="lineTableBudgetTd" align="right" width="200px">
									<span style="font-weight:bold; ">
										<xsl:text>Hesaplanan </xsl:text>
										<xsl:value-of select="cac:TaxCategory/cac:TaxScheme/cbc:Name"/>
										<xsl:text>(%</xsl:text>
										<xsl:value-of select="cbc:Percent"/>
										<xsl:text>) (TL)</xsl:text>
									</span>
								</td>
								<td id="lineTableBudgetTd" style="width:81px; " align="right">
									<span>
										<xsl:value-of select="format-number(cbc:TaxAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
										<xsl:text> TL</xsl:text>
									</span>
								</td>
							</tr>
						</xsl:if>
					</xsl:for-each>
					<xsl:for-each select="n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal">
						<xsl:if test="//n1:Invoice/cbc:DocumentCurrencyCode != 'TRY' and cbc:TaxAmount != ''">
							<tr id="budgetContainerTr" align="right">
								<td id="budgetContainerDummyTd"/>
								<td id="lineTableBudgetTd" width="211px" align="right">
									<span style="font-weight:bold; ">
										<xsl:text>KDV Tevkifat-[</xsl:text>
										<xsl:value-of select="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode"/>
										<xsl:text>]-</xsl:text>
										<xsl:text>(%</xsl:text>
										<xsl:value-of select="cbc:Percent"/>
										<xsl:text>) (TL)</xsl:text>
									</span>
								</td>
								<td id="lineTableBudgetTd" style="width:82px; " align="right">
									<xsl:for-each select="cac:TaxCategory/cac:TaxScheme">
										<xsl:text> </xsl:text>
										<xsl:value-of select="format-number(../../cbc:TaxAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
										<xsl:text> TL</xsl:text>
									</xsl:for-each>
								</td>
							</tr>
						</xsl:if>
					</xsl:for-each>
					<xsl:if test="//n1:Invoice/cbc:DocumentCurrencyCode != 'TRY'">
						<tr align="right">
							<td/>
							<td id="lineTableBudgetTd" align="right" width="200px">
								<span style="font-weight:bold; ">
									<xsl:text>Mal Hizmet Toplam Tutarı(TL)</xsl:text>
								</span>
							</td>
							<td id="lineTableBudgetTd" style="width:81px; " align="right">
								<span>
									<xsl:value-of select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
									<xsl:text> TL</xsl:text>
								</span>
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
								<xsl:value-of select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
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
								<xsl:value-of select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
								<xsl:text> TL</xsl:text>
							</td>
						</tr>
					</xsl:if>
				</table>
				<br/>
				<xsl:if test="count(n1:Invoice/cac:DespatchDocumentReference/cbc:ID) &gt; 1">
					<table id="irsaliye" style="border-collapse:collapse;width:800px; padding:10px 0px;color:black; margin-bottom: 5px;">
						<tr>
							<td align="left" style="padding: 4px 5px; background-color: rgb(214, 214, 214); border: 1px solid gray;width:135px;">
								<span style="font-weight:bold; ">
									<xsl:text>İrsaliye No ve Tarihleri :</xsl:text>
								</span>
							</td>
							<td style="padding: 4px 5px; border: 1px solid gray;">
								<xsl:for-each select="n1:Invoice/cac:DespatchDocumentReference">
									<xsl:if test="cbc:ID !='' and cbc:IssueDate !=''">
										<xsl:value-of select="cbc:ID"/>
										<xsl:text>&#xA0;&#xA0;(</xsl:text>
										<xsl:value-of select="substring(cbc:IssueDate,9,2)"/>-<xsl:value-of select="substring(cbc:IssueDate,6,2)"/>-<xsl:value-of select="substring(cbc:IssueDate,1,4)"/>
										<xsl:text>)</xsl:text>
										<xsl:if test="position() != last()">
											<xsl:text>&#xA0;&#xA0;|&#xA0;&#xA0;</xsl:text>
										</xsl:if>
									</xsl:if>
								</xsl:for-each>
							</td>
						</tr>
					</table>
				</xsl:if>
				<xsl:if test="count(n1:Invoice/cac:OrderReference/cbc:ID) &gt; 1">
					<table id="siparis" style="border-collapse:collapse;width:800px; padding:10px 0px;color:black;">
						<tr>
							<td align="left" style="padding: 4px 5px; background-color: rgb(214, 214, 214);; border: 1px solid gray;width:135px; ">
								<span style="font-weight:bold; ">
									<xsl:text>Siparis No ve Tarihleri :</xsl:text>
								</span>
							</td>
							<td style="padding: 4px 5px; border: 1px solid gray;">
								<xsl:for-each select="n1:Invoice/cac:OrderReference">
									<xsl:if test="cbc:ID !='' and cbc:IssueDate !=''">
										<xsl:value-of select="cbc:ID"/>
										<xsl:text>&#xA0;&#xA0;(</xsl:text>
										<xsl:value-of select="substring(cbc:IssueDate,9,2)"/>-<xsl:value-of select="substring(cbc:IssueDate,6,2)"/>-<xsl:value-of select="substring(cbc:IssueDate,1,4)"/>
										<xsl:text>)</xsl:text>
										<xsl:if test="position() != last()">
											<xsl:text>&#xA0;&#xA0;|&#xA0;&#xA0;</xsl:text>
										</xsl:if>
									</xsl:if>
								</xsl:for-each>
							</td>
						</tr>
					</table>
				</xsl:if>
				<br/>
				<table id="notesTable" width="800" height="100">
					<tbody>
						<tr align="left">
							<td id="notesTableTd" style="padding:5px;">
									<xsl:choose>
										<xsl:when test="//n1:Invoice/cbc:InvoiceTypeCode = 'IHRACKAYITLI'">
											<xsl:for-each select="//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
												<xsl:if test="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015' and cac:TaxCategory/cbc:TaxExemptionReason != ''">
													<b>Vergi İstisna Muafiyet Sebebi:&#xA0;</b>
													<xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReason"/>
													<br/>
												</xsl:if>
											</xsl:for-each>
										</xsl:when>
										<xsl:otherwise>
											<xsl:for-each select="//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
												<xsl:if test="cbc:TaxAmount=0 and cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015' and cac:TaxCategory/cbc:TaxExemptionReason != ''">
													<b>Vergi İstisna Muafiyet Sebebi:&#xA0;</b>
													<xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReason"/>
													<br/>
												</xsl:if>
											</xsl:for-each>
										</xsl:otherwise>
									</xsl:choose>
								<xsl:for-each select="//n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
									<xsl:if test="cbc:TaxTypeCode != '' or cbc:Name != ''">
										<b>Tevkifat Sebebi:&#xA0;</b>
										<xsl:value-of select="cbc:TaxTypeCode"/>
										<xsl:text>-</xsl:text>
										<xsl:value-of select="cbc:Name"/>
										<br/>
									</xsl:if>
								</xsl:for-each>
								<xsl:for-each select="n1:Invoice/cbc:Note">
									<xsl:value-of select="."/>
									<br/>
								</xsl:for-each>
								<xsl:if test="//n1:Invoice/cac:PaymentMeans/cbc:InstructionNote">
									<b>Ödeme Notu:</b>
									<xsl:value-of select="//n1:Invoice/cac:PaymentMeans/cbc:InstructionNote"/>
									<br/>
								</xsl:if>
								<xsl:if test="//n1:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:PaymentNote">
									<b>Hesap Açıklaması:</b>
									<xsl:value-of select="//n1:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:PaymentNote"/>
									<br/>
								</xsl:if>
								<xsl:if test="//n1:Invoice/cac:PaymentTerms/cbc:Note!=''">
									<b>Ödeme Koşulu:</b>
									<xsl:value-of select="//n1:Invoice/cac:PaymentTerms/cbc:Note"/>
									<br/>
								</xsl:if>
							</td>
						</tr>
					</tbody>
				</table>
				<table id="hesapBilgileri" style="padding: 5px 0px; width:100%; margin-top:5px;width:800px;">
					<tbody>
						<tr>
							<td style="width:100%">
								<fieldset style="padding: 3px;border: 1px solid black;">
									<legend style="background-color:white;background-color:white;text-align:center;">
										<b>Hesap Bilgileri</b>
									</legend>
									<table style="width:100%">
										<tbody>
											<tr>
												<td style="width:100%;padding:4px;"></td>
											</tr>
										</tbody>
									</table>
								</fieldset>
							</td>
						</tr>
					</tbody>
				</table>
        <table style="width:800px;">
          <tbody>
            <tr>
              <td style="font-weight: bold; font-size: 12px; text-align:right;padding-top:10px;">e-Arşiv izni kapsamında elektronik ortamda iletilmiştir.</td>
            </tr>
            <tr>
              <td style="font-weight: bold; font-size: 12px; text-align:right;padding-top:10px;padding-bottom:10px;">İRSALİYE YERİNE GEÇER.</td>
            </tr>
          </tbody>
        </table>
			</body>
		</html>
	</xsl:template>
	<xsl:template match="dateFormatter">
		<xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
	</xsl:template>
	<xsl:template match="//n1:Invoice/cac:InvoiceLine">
		<tr id="lineTableTr">
			<td id="lineTableTd">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:value-of select="./cbc:ID"/>
				</span>
			</td>
			<td id="lineTableTd">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:value-of select="./cac:Item/cbc:Name"/>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:value-of select="format-number(./cbc:InvoicedQuantity, '###.##0,####', 'european')"/>
					<xsl:if test="./cbc:InvoicedQuantity/@unitCode">
						<xsl:for-each select="./cbc:InvoicedQuantity">
							<xsl:text/>
							<xsl:choose>
								<xsl:when test="@unitCode  = 'C62'">
									<span>
										<xsl:text> Adet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'NIU'">
									<span>
										<xsl:text> Adet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CPR'">
									<span>
										<xsl:text> Adet-Çift</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'AS'">
									<span>
										<xsl:text> Asorti</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MON'">
									<span>
										<xsl:text> Ay</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'FOT'">
									<span>
										<xsl:text> Ayak</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'D92'">
									<span>
										<xsl:text> Bant</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BAR'">
									<span>
										<xsl:text> Bar</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BR'">
									<span>
										<xsl:text> Bar</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BAS'">
									<span>
										<xsl:text> Bas</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'EA'">
									<span>
										<xsl:text> Beher</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = '2W'">
									<span>
										<xsl:text> Bidon</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'T3'">
									<span>
										<xsl:text> Bin Adet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'TWH'">
									<span>
										<xsl:text> Bin KWH</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'D40'">
									<span>
										<xsl:text> Bin Lt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'R9'">
									<span>
										<xsl:text> Bin M³</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = '4A'">
									<span>
										<xsl:text> Bobin</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CL'">
									<span>
										<xsl:text> Bobin</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'E4'">
									<span>
										<xsl:text> Brüt KG</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'GD'">
									<span>
										<xsl:text> Brüt Varil</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'GRO'">
									<span>
										<xsl:text> Brüt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'AD'">
									<span>
										<xsl:text> Byte</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CGM'">
									<span>
										<xsl:text> Cgm</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CLT'">
									<span>
										<xsl:text> CLt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CMT'">
									<span>
										<xsl:text> CM</xsl:text>
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
								<xsl:when test="@unitCode  = 'PR'">
									<span>
										<xsl:text> Çift</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'HA'">
									<span>
										<xsl:text> Çile</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'RD'">
									<span>
										<xsl:text> Çubuk</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'SA'">
									<span>
										<xsl:text> Çuval</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'D79'">
									<span>
										<xsl:text> Demet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'A49'">
									<span>
										<xsl:text> Denye</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'DC'">
									<span>
										<xsl:text> Disk</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'D61'">
									<span>
										<xsl:text> DK</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MIN'">
									<span>
										<xsl:text> DK</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'DLT'">
									<span>
										<xsl:text> DLt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'DMT'">
									<span>
										<xsl:text> DM</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'DMK'">
									<span>
										<xsl:text> DM²</xsl:text>
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
								<xsl:when test="@unitCode  = 'Z3'">
									<span>
										<xsl:text> Fıçı</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BH'">
									<span>
										<xsl:text> Fırça</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'GB'">
									<span>
										<xsl:text> Galon</xsl:text>
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
								<xsl:when test="@unitCode  = 'GRM'">
									<span>
										<xsl:text> Gr</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'GN'">
									<span>
										<xsl:text> Gross Galon</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'GT'">
									<span>
										<xsl:text> Gross Ton</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = '10'">
									<span>
										<xsl:text> Grup</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'DAY'">
									<span>
										<xsl:text> Gün</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'RG'">
									<span>
										<xsl:text> Halka</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'INH'">
									<span>
										<xsl:text> İnç</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = '4B'">
									<span>
										<xsl:text> Kap</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'NCR'">
									<span>
										<xsl:text> Karat</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'Z2'">
									<span>
										<xsl:text> Kasa/Sandık</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'D66'">
									<span>
										<xsl:text> Kaset</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = '2P'">
									<span>
										<xsl:text> Kilobyte</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'KGM'">
									<span>
										<xsl:text> Kilogram</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'KWT'">
									<span>
										<xsl:text> Kilowatt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'IE'">
									<span>
										<xsl:text> Kişi</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'KJO'">
									<span>
										<xsl:text> KJO</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'K6'">
									<span>
										<xsl:text> KLT</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'KTM'">
									<span>
										<xsl:text> Km</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'KMK'">
									<span>
										<xsl:text> Km²</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'AB'">
									<span>
										<xsl:text> Koli</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CT'">
									<span>
										<xsl:text> Koli</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CH'">
									<span>
										<xsl:text> Konteyner</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BJ'">
									<span>
										<xsl:text> Kova</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'PL'">
									<span>
										<xsl:text> Kova</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CU'">
									<span>
										<xsl:text> Kupa</xsl:text>
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
								<xsl:when test="@unitCode  = 'CS'">
									<span>
										<xsl:text> Kutu</xsl:text>
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
								<xsl:when test="@unitCode  = 'KWH'">
									<span>
										<xsl:text> KWH</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'LTR'">
									<span>
										<xsl:text> Litre</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'RL'">
									<span>
										<xsl:text> Makara</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'SO'">
									<span>
										<xsl:text> Makara</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MAW'">
									<span>
										<xsl:text> Megawatt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MGM'">
									<span>
										<xsl:text> MGM</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = '77'">
									<span>
										<xsl:text> Miliinç</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MLT'">
									<span>
										<xsl:text> MLt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MMT'">
									<span>
										<xsl:text> Mm</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MMQ'">
									<span>
										<xsl:text> Mm³</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MTR'">
									<span>
										<xsl:text> Mt</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MTK'">
									<span>
										<xsl:text> Mt²</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MTQ'">
									<span>
										<xsl:text> Mt³</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'MWH'">
									<span>
										<xsl:text> MWH</xsl:text>
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
								<xsl:when test="@unitCode  = 'D97'">
									<span>
										<xsl:text> Palet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'PF'">
									<span>
										<xsl:text> Palet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BD'">
									<span>
										<xsl:text> Pano</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'PG'">
									<span>
										<xsl:text> Plaka</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'RO'">
									<span>
										<xsl:text> Rulo</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'HUR'">
									<span>
										<xsl:text> Saat</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'ST'">
									<span>
										<xsl:text> Sayfa</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BK'">
									<span>
										<xsl:text> Sepet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'SET'">
									<span>
										<xsl:text> Set</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'CY'">
									<span>
										<xsl:text> Silindir</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'D62'">
									<span>
										<xsl:text> Sn</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BO'">
									<span>
										<xsl:text> Şişe</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'LR'">
									<span>
										<xsl:text> Tabaka</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'TN'">
									<span>
										<xsl:text> Teneke</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = '26'">
									<span>
										<xsl:text> Ton</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'AA'">
									<span>
										<xsl:text> Top</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BG'">
									<span>
										<xsl:text> Torba/Poşet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'TU'">
									<span>
										<xsl:text> Tüp</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BLD'">
									<span>
										<xsl:text> Varil</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'BLL'">
									<span>
										<xsl:text> Varil</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'DR'">
									<span>
										<xsl:text> Varil</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'ANN'">
									<span>
										<xsl:text> Yıl</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'H62'">
									<span>
										<xsl:text> Yüz Adet</xsl:text>
									</span>
								</xsl:when>
								<xsl:when test="@unitCode  = 'EV'">
									<span>
										<xsl:text> Zarf</xsl:text>
									</span>
								</xsl:when>
							</xsl:choose>
						</xsl:for-each>
					</xsl:if>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:call-template name="Curr_Type">
						<xsl:with-param name="valuePath" select="./cac:Price/cbc:PriceAmount"/>
						<xsl:with-param name="format" select="'###.##0,00000000'"/>
					</xsl:call-template>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#160;</xsl:text>
					<xsl:for-each select="./cac:AllowanceCharge/cbc:MultiplierFactorNumeric">
						<xsl:value-of select="format-number(. * 100, '###.##0,00', 'european')"/>
						<xsl:if test="position() != last()">
							<xsl:text> | </xsl:text>
						</xsl:if>
					</xsl:for-each>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#160;</xsl:text>
					<xsl:for-each select="cac:AllowanceCharge/cbc:Amount">
						<xsl:value-of select="format-number(., '###.##0,00', 'european')"/>
						<xsl:if test="position() != last()">
							<xsl:text> | </xsl:text>
						</xsl:if>
					</xsl:for-each>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:for-each select="./cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
						<xsl:if test="cbc:TaxTypeCode='0015' ">
							<xsl:text/>
							<xsl:if test="../../cbc:Percent">
								<xsl:text> %</xsl:text>
								<xsl:value-of select="format-number(../../cbc:Percent, '###.##0,00', 'european')"/>
							</xsl:if>
						</xsl:if>
					</xsl:for-each>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:for-each select="./cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
						<xsl:if test="cbc:TaxTypeCode='0015' ">
							<xsl:text/>
							<xsl:call-template name="Curr_Type">
								<xsl:with-param name="valuePath" select="../../cbc:TaxAmount"/>
								<xsl:with-param name="format" select="'###.##0,00'"/>
							</xsl:call-template>
						</xsl:if>
					</xsl:for-each>
				</span>
			</td>
			<td id="lineTableTd" style="font-size: xx-small" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:for-each select="./cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
						<xsl:if test="cbc:TaxTypeCode!='0015' ">
							<xsl:text/>
							<xsl:value-of select="cbc:Name"/>
							<xsl:if test="../../cbc:Percent">
								<xsl:text> (%</xsl:text>
								<xsl:value-of select="format-number(../../cbc:Percent, '###.##0,00', 'european')"/>
								<xsl:text>)=</xsl:text>
							</xsl:if>
							<xsl:call-template name="Curr_Type">
								<xsl:with-param name="valuePath" select="../../cbc:TaxAmount"/>
								<xsl:with-param name="format" select="'###.##0,00'"/>
							</xsl:call-template>
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
							<xsl:call-template name="Curr_Type">
								<xsl:with-param name="valuePath" select="."/>
								<xsl:with-param name="format" select="'###.##0,00'"/>
							</xsl:call-template>
						</xsl:for-each>
						<xsl:if test="not(position() = last())">
							<br/>
						</xsl:if>
					</xsl:for-each>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
					<xsl:choose>
						<xsl:when test="./cac:AllowanceCharge[cbc:ChargeIndicator = 'false' or cbc:ChargeIndicator = 'FALSE']/cbc:BaseAmount">
							<xsl:call-template name="Curr_Type">
								<xsl:with-param name="valuePath" select="(./cac:AllowanceCharge[cbc:ChargeIndicator = 'false' or cbc:ChargeIndicator = 'FALSE']/cbc:BaseAmount)[1]"/>
								<xsl:with-param name="format" select="'###.##0,00'"/>
							</xsl:call-template>
						</xsl:when>
						<xsl:otherwise>
							<xsl:call-template name="Curr_Type">
								<xsl:with-param name="valuePath" select="./cbc:LineExtensionAmount"/>
								<xsl:with-param name="format" select="'###.##0,00'"/>
							</xsl:call-template>
						</xsl:otherwise>
					</xsl:choose>
				</span>
			</td>
		</tr>
	</xsl:template>
	<xsl:template match="//n1:Invoice">
		<tr id="lineTableTr">
			<td id="lineTableTd">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
			<td id="lineTableTd" align="right">
				<span>
					<xsl:text>&#xA0;</xsl:text>
				</span>
			</td>
		</tr>
	</xsl:template>
	<xsl:template name="Curr_Type">
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
