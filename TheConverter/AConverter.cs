using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using XmlData;
using GameSaveInfo;
namespace GSMConverter {
    abstract class AConverter {
        private const string unescaped_ampersand = "&([^;\\W]*([^;\\w]|$))";
        private const string replace_ampersands = "&amp;$1";

        protected XmlFile xml_file;

        public GameXmlFile output;
        public XmlElement DocumentElement {
            get {
                return xml_file.DocumentElement;
            }
        }

        protected AConverter(string xml) {
            FileInfo file = new FileInfo(Path.GetTempFileName());
            output = new GameXmlFile(file);
            xml_file = new XmlFile(CleanUpInput(xml));
            
        }

        protected virtual string CleanUpInput(string input) {
            input = Regex.Replace(input, unescaped_ampersand, replace_ampersands);
            return input;
        }


        public XmlDocument export() {
            return output;
        }

        protected string generateName(string input) {
            return Game.prepareGameName(input);
        }

        private void example(XmlElement entry) {
            foreach (XmlAttribute attr in entry.Attributes) {
                switch (attr.Name) {
                    default:
                        throw new NotSupportedException(attr.Name);
                }
            }
            foreach (XmlElement ele in entry.ChildNodes) {
                switch (ele.Name) {
                    default:
                        throw new NotSupportedException(ele.Name);
                }

            }
        }

    }
}
