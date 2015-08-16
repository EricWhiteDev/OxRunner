﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using OxRunner;
using OpenXmlPowerTools;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace OxRunner
{
    public class SystemIOPackagingTest
    {
        public static XElement DoTest(Repo repo, string guidName)
        {
            try
            {
                var xml = RegenerateUsingSystemIoPackaging(repo, guidName);
                return xml;
            }
            catch (PowerToolsDocumentException e)
            {
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XElement("PowerToolsDocumentException",
                        TakeOnlyFirstLine(PtUtils.MakeValidXml(e.ToString()))));
                return errorXml;
            }
            catch (FileFormatException e)
            {
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XElement("FileFormatException",
                        TakeOnlyFirstLine(PtUtils.MakeValidXml(e.ToString()))));
                return errorXml;
            }
            catch (Exception e)
            {
                var errorXml = new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XElement("Exception",
                        TakeOnlyFirstLine(PtUtils.MakeValidXml(e.ToString()))));
                return errorXml;
            }
        }

        private static string TakeOnlyFirstLine(string p)
        {
            string newP = p.Split(new [] { "_D__A_" }, StringSplitOptions.None)[0];
            return newP;
        }

        private static XElement RegenerateUsingSystemIoPackaging(Repo repo, string guidName)
        {
            try
            {
                var repoItem = repo.GetRepoItemByteArray(guidName);
                return GenerateNewOpenXmlFile(guidName, repoItem.ByteArray, repoItem.Extension);
            }
            catch (Exception e)
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XAttribute("Error", true),
                    new XAttribute("ErrorDescription", "Exception thrown (1)"),
                    TakeOnlyFirstLine(PtUtils.MakeValidXml(e.ToString())));
            }
        }

        private static XElement GenerateNewOpenXmlFile(string guidName, byte[] byteArray, string extension)
        {
            try
            {
                ValidationErrors valErrors1 = null;
                ValidationErrors valErrors2 = null;

                if (Util.IsWordprocessingML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(byteArray, 0, byteArray.Length);
                        using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, true))
                        {
                            valErrors1 = ValidateAgainstAllVersions(wDoc);
                        }
                    }
                }
                else if (Util.IsSpreadsheetML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(byteArray, 0, byteArray.Length);
                        using (SpreadsheetDocument sDoc = SpreadsheetDocument.Open(ms, true))
                        {
                            valErrors1 = ValidateAgainstAllVersions(sDoc);
                        }
                    }
                }
                else if (Util.IsPresentationML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(byteArray, 0, byteArray.Length);
                        using (PresentationDocument pDoc = PresentationDocument.Open(ms, true))
                        {
                            valErrors1 = ValidateAgainstAllVersions(pDoc);
                        }
                    }
                }
                else
                {
                    return new XElement("Document",
                        new XAttribute("GuidName", guidName),
                        new XAttribute("Error", true),
                        new XAttribute("ErrorDescription", "Not one of the three Open XML document types."));
                }

                byte[] newByteArray = ClonePackage(byteArray);

                if (Util.IsWordprocessingML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(newByteArray, 0, newByteArray.Length);
                        using (WordprocessingDocument wDoc = WordprocessingDocument.Open(ms, true))
                        {
                            valErrors2 = ValidateAgainstAllVersions(wDoc);
                        }
                    }
                }
                else if (Util.IsSpreadsheetML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(newByteArray, 0, newByteArray.Length);
                        using (SpreadsheetDocument sDoc = SpreadsheetDocument.Open(ms, true))
                        {
                            valErrors2 = ValidateAgainstAllVersions(sDoc);
                        }
                    }
                }
                else if (Util.IsPresentationML(extension))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(newByteArray, 0, newByteArray.Length);
                        using (PresentationDocument pDoc = PresentationDocument.Open(ms, true))
                        {
                            valErrors2 = ValidateAgainstAllVersions(pDoc);
                        }
                    }
                }
                return GetValidationReport(guidName, valErrors1, valErrors2);
            }
            catch (Exception e)
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XAttribute("Error", true),
                    new XAttribute("ErrorDescription", "Exception thrown when opening document"),
                    TakeOnlyFirstLine(PtUtils.MakeValidXml(e.ToString())));
            }
        }

        static byte[] ClonePackage(byte[] fromByteArray)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(fromByteArray, 0, fromByteArray.Length);
                using (Package pkg = Package.Open(ms, FileMode.Open, FileAccess.Read))
                using (MemoryStream newMs = new MemoryStream())
                {
                    using (Package newPkg = Package.Open(newMs, FileMode.Create, FileAccess.ReadWrite))
                    {
                        foreach (var part in pkg.GetParts())
                        {
                            if (part.ContentType != "application/vnd.openxmlformats-package.relationships+xml")
                            {
                                var newPart = newPkg.CreatePart(part.Uri, part.ContentType, CompressionOption.Normal);
                                using (var oldStream = part.GetStream())
                                using (var newStream = newPart.GetStream())
                                    CopyStream(oldStream, newStream);
                                foreach (var rel in part.GetRelationships())
                                    newPart.CreateRelationship(rel.TargetUri, rel.TargetMode, rel.RelationshipType, rel.Id);
                            }
                        }
                        foreach (var rel in pkg.GetRelationships())
                            newPkg.CreateRelationship(rel.TargetUri, rel.TargetMode, rel.RelationshipType, rel.Id);
                    }
                    return newMs.ToArray();
                }
            }
        }

        private static void CopyStream(Stream source, Stream target)
        {
            const int BufSize = 0x4096;
            byte[] buf = new byte[BufSize];
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, BufSize)) > 0)
                target.Write(buf, 0, bytesRead);
        }

        private static XElement GetValidationReport(string guidName, ValidationErrors valErrors1, ValidationErrors valErrors2)
        {
            XElement v2007 = GetDeltaInOneErrorList(valErrors1.Office2007Errors, valErrors2.Office2007Errors, "Office2007Errors");
            XElement v2010 = GetDeltaInOneErrorList(valErrors1.Office2010Errors, valErrors2.Office2010Errors, "Office2010Errors");
            XElement v2013 = GetDeltaInOneErrorList(valErrors1.Office2013Errors, valErrors2.Office2013Errors, "Office2013Errors");
            bool haveErrors = v2007 != null || v2010 != null || v2013 != null;
            XAttribute errorAtt = null;
            XAttribute errorDescription = null;
            if (haveErrors)
            {
                errorAtt = new XAttribute("Error", true);
                errorDescription = new XAttribute("ErrorDescription", "Difference in validation errors before vs. after (only 1st 3 errors listed)");
            }
            var docElement = new XElement("Document",
                new XAttribute("GuidName", guidName),
                errorAtt,
                errorDescription,
                v2007,
                v2010,
                v2013);
            return docElement;
        }

        private static XElement GetDeltaInOneErrorList(List<ValidationErrorInfo> errorList1, List<ValidationErrorInfo> errorList2, XName errorElementName)
        {
            if (errorList1.Count() != errorList2.Count() ||
                errorList1.Zip(errorList2, (e1, e2) => new
                {
                    Error1 = e1,
                    Error2 = e2,
                })
                .Any(p =>
                {
                    if (p.Error1.ToString() == p.Error2.ToString())
                        return false;
                    return true;
                }))
            {
                XElement deltaErrors = new XElement(errorElementName,
                    new XElement("Before",
                        SerializeErrors(errorList1)),
                    new XElement("After",
                        SerializeErrors(errorList2)));
                return deltaErrors;
            }
            return null;
        }

        private static string SerializeErrors(IEnumerable<ValidationErrorInfo> errorList)
        {
            return errorList.Take(3).Select(err =>
            {
                StringBuilder sb = new StringBuilder();
                if (err.Description.Length > 300)
                    sb.Append(PtUtils.MakeValidXml(err.Description.Substring(0, 300) + " ... elided ...") + Environment.NewLine);
                else
                    sb.Append(PtUtils.MakeValidXml(err.Description) + Environment.NewLine);
                sb.Append("  in part " + PtUtils.MakeValidXml(err.Part.Uri.ToString()) + Environment.NewLine);
                sb.Append("  at " + PtUtils.MakeValidXml(err.Path.XPath) + Environment.NewLine);
                return sb.ToString();
            })
            .StringConcatenate();
        }

        private static XElement ValidateAgainstAllFormatsGenerateErrorXml(string guidName, OpenXmlPackage oxDoc)
        {
            List<XElement> errorElements = new List<XElement>();
            bool pass = ValidateAgainstSpecificVersionGenerateErrorXml(oxDoc, errorElements, FileFormatVersions.Office2007, H.SdkValidationError2007) &&
                ValidateAgainstSpecificVersionGenerateErrorXml(oxDoc, errorElements, FileFormatVersions.Office2010, H.SdkValidationError2010) &&
                ValidateAgainstSpecificVersionGenerateErrorXml(oxDoc, errorElements, FileFormatVersions.Office2013, H.SdkValidationError2013);
            if (pass)
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName));
            }
            else
            {
                return new XElement("Document",
                    new XAttribute("GuidName", guidName),
                    new XAttribute("Error", true),
                    new XAttribute("ErrorDescription", "Generated document failed to validate"),
                    errorElements);
            }
        }

        private static bool ValidateAgainstSpecificVersionGenerateErrorXml(OpenXmlPackage oxDoc, List<XElement> errorElements, DocumentFormat.OpenXml.FileFormatVersions versionToValidateAgainst, XName versionSpecificMetricName)
        {
            OpenXmlValidator validator = new OpenXmlValidator(versionToValidateAgainst);
            var errors = validator.Validate(oxDoc);
            bool valid = errors.Count() == 0;
            if (!valid)
            {
                errorElements.Add(new XElement(versionSpecificMetricName, new XAttribute(H.Val, true),
                    errors.Take(3).Select(err =>
                    {
                        StringBuilder sb = new StringBuilder();
                        if (err.Description.Length > 300)
                            sb.Append(PtUtils.MakeValidXml(err.Description.Substring(0, 300) + " ... elided ...") + Environment.NewLine);
                        else
                            sb.Append(PtUtils.MakeValidXml(err.Description) + Environment.NewLine);
                        sb.Append("  in part " + PtUtils.MakeValidXml(err.Part.Uri.ToString()) + Environment.NewLine);
                        sb.Append("  at " + PtUtils.MakeValidXml(err.Path.XPath) + Environment.NewLine);
                        return sb.ToString();
                    })));
            }
            return valid;
        }

        private class ValidationErrors
        {
            public List<ValidationErrorInfo> Office2007Errors = new List<ValidationErrorInfo>();
            public List<ValidationErrorInfo> Office2010Errors = new List<ValidationErrorInfo>();
            public List<ValidationErrorInfo> Office2013Errors = new List<ValidationErrorInfo>();
        }

        private static ValidationErrors ValidateAgainstAllVersions(OpenXmlPackage oxDoc)
        {
            ValidationErrors validationErrors = new ValidationErrors();
            OpenXmlValidator validator = new OpenXmlValidator(FileFormatVersions.Office2007);
            validationErrors.Office2007Errors = validator.Validate(oxDoc).Where(err => UnexpectedError(err)).ToList();
            validator = new OpenXmlValidator(FileFormatVersions.Office2010);
            validationErrors.Office2010Errors = validator.Validate(oxDoc).Where(err => UnexpectedError(err)).ToList();
            validator = new OpenXmlValidator(FileFormatVersions.Office2013);
            validationErrors.Office2013Errors = validator.Validate(oxDoc).Where(err => UnexpectedError(err)).ToList();
            return validationErrors;
        }

        private static string[] ExpectedErrors = new string[] {
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstRow' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastRow' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstCol' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastCol' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:firstColumn' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:lastColumn' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:noHBand' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:noVBand' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band1Vert' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band2Vert' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band1Horz' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:band2Horz' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:neCell' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:nwCell' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:seCell' attribute is not declared",
            "The 'http://schemas.openxmlformats.org/wordprocessingml/2006/main:swCell' attribute is not declared",
            "The root XML element \"http://schemas.openxmlformats.org/drawingml/2006/diagram:drawing\" in the part is incorrect.",
        };

        private static bool UnexpectedError(ValidationErrorInfo err)
        {
            var errStr = err.Description;
            if (ExpectedErrors.Any(e => errStr.Contains(e)))
                return false;
            return true;
        }

    }
}
