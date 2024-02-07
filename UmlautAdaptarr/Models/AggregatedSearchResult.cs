using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace UmlautAdaptarr.Models
{
    public class AggregatedSearchResult
    {
        public XDocument ContentDocument { get; private set; }
        public string ContentType { get; set; }
        public Encoding ContentEncoding { get; set; }
        private HashSet<string> _uniqueItems;

        public AggregatedSearchResult(string contentType, Encoding contentEncoding)
        {
            ContentType = contentType;
            ContentEncoding = contentEncoding;
            _uniqueItems = new HashSet<string>();

            // Initialize ContentDocument with a basic RSS structure
            ContentDocument = new XDocument(new XElement("rss", new XElement("channel")));
        }

        public string Content => ContentDocument.ToString();

        public void AggregateItems(string content)
        {
            var xDoc = XDocument.Parse(content);
            var items = xDoc.Descendants("item");

            foreach (var item in items)
            {
                var itemAsString = item.ToString();
                if (_uniqueItems.Add(itemAsString))
                {
                    ContentDocument.Root.Element("channel").Add(item);
                }
                else
                {

                }
            }
        }
    }
}
