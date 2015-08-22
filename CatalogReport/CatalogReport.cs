using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRunner
{
    class CatalogReport
    {
        static FileInfo s_CatalogFileName = new FileInfo(@"C:\Users\Eric\Documents\OxRunner\Catalog-15-08-21-130826013.log");

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Load");
            var xDoc = XDocument.Load(s_CatalogFileName.FullName);
            Console.WriteLine("Getting list of namespaces");
            var nsList = xDoc.Root.Elements("Documents").Elements("Document").Elements("Namespaces").Elements("Namespace").Select(ns => ns.Attribute("NamespacePrefix").Value + ":" + ns.Attribute("NamespaceName").Value);
            Console.WriteLine("Starting Sort / Distinct");
            var sortedDistinct = nsList.OrderBy(n => n).Distinct().ToList();
            var newXDoc = new XElement("Report",
                new XElement("Namespaces",
                    sortedDistinct.Select(n => new XElement("Namespace", new XAttribute("Val", n)))));
            Console.WriteLine(newXDoc);
        }
    }
}
