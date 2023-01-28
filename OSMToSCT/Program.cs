using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace OSMToSCT
{
    class Program
    {
        static void Main(string[] args)
        {
            DirectoryInfo dir;
            FileInfo singleFile;
            String path;

            //check filename
            if (args.Length < 1)
            {
                Console.WriteLine("No path specified. Enter path.");
                path = Console.ReadLine().Trim('"', '\'');
            }
            else
            {
                path = args[0].Trim('"', '\'');
            }

            try
            {
                dir = new DirectoryInfo(path);
            }
            catch (ArgumentException argException)
            {
                Console.WriteLine("Error: " + argException.Message);
                Console.ReadLine();
                return;
            }
            Console.WriteLine();

            //check regionname
            Console.WriteLine("Enter region name.");
            String regionname = Console.ReadLine();
            Console.WriteLine();

            Console.WriteLine("Select region type");
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine("0: Apron");
            Console.WriteLine("1: Building");
            Console.WriteLine("2: Grass");
            Console.WriteLine("3: Taxiway");
            Console.WriteLine("4: Runway");
            Console.WriteLine("5: Background");
            Console.WriteLine("6: Stand / Holding Point");
            Console.WriteLine("7: White");
            Console.WriteLine("8: Yellow");
            Console.WriteLine("9: Red");
            Console.WriteLine("10: Road");
            Console.WriteLine("11: Disused");
            Console.WriteLine("12: Coast");
            Console.WriteLine("13: Danger");
            Console.WriteLine("----------------------------------------------");
            Int32 regiontype = Int32.Parse( Console.ReadLine() );

            if (!dir.Exists)
            {
                singleFile = new FileInfo(path);

                if (singleFile.Exists)
                {
                    ConvertToSCT(singleFile, regionname, regiontype);
                    Console.WriteLine("Done. Press enter to close.");
                    Console.ReadLine();
                    return;
                }
                else
                {
                    Console.WriteLine("Invalid path. Press enter to continue.");
                    Console.Read();
                    return;
                }
            }

            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Name.ToUpper().Contains(".OSM") || file.Name.ToUpper().Contains(".XML"))
                {
                    Console.WriteLine("Converting " + file.Name);
                    ConvertToSCT(file, regionname, regiontype);
                }
                else
                {
                    Console.WriteLine("Skipping " + file.Name);
                }
            }

            Console.WriteLine("Done. Press enter to close.");
            Console.ReadLine();
        }

        protected static void ConvertToSCT(FileInfo file, String regionname, int areaType)
        {
            FileInfo newFile;
            StreamWriter newFileWriter;
            XPathDocument xpDoc;
            XPathNavigator xpNav;
            XPathNodeIterator xpNodeIterator;
            List<int> nodeOrderList;
            Dictionary<int, Point> nodeDict;
            decimal decLatitude;
            decimal decLongitude;
            int nodeId;

            String[] regiondefs = { "Apron:smrGDapron", "Building:smrGDbuilding", "Grass:smrGDgrass", "Taxiway:smrGDtaxiway", "Runway:smrGDrunway", "Background:smrGDbackground", "Stand/Hold:standHold", "White:White", "Yellow:smrYellow", "Red:smrRed", "Road:smrRoad", "Disused:smrGDdisused", "Coastline:coast1", "Danger:danger" };
            String[] regiondef = regiondefs[areaType].Split(':');

            nodeOrderList = new List<int>();
            nodeDict = new Dictionary<int, Point>();

            newFile = new FileInfo(file.FullName.Replace(".osm", "").Replace(".xml", "") + ".txt");
            newFileWriter = newFile.CreateText();

            try
            {
                xpDoc = new XPathDocument(file.OpenRead());
                xpNav = xpDoc.CreateNavigator();

                // Iterate throught the node definitions
                xpNodeIterator = xpNav.Select("/osm/node");

                while (xpNodeIterator.MoveNext())
                {
                    try
                    {
                        nodeId = Int32.Parse(xpNodeIterator.Current.GetAttribute("id", ""));
                        decLatitude = Decimal.Parse(xpNodeIterator.Current.GetAttribute("lat", ""), CultureInfo.InvariantCulture);
                        decLongitude = Decimal.Parse(xpNodeIterator.Current.GetAttribute("lon", ""), CultureInfo.InvariantCulture);

                        nodeDict.Add(nodeId, new Point() { Latitude = decLatitude, Longitude = decLongitude });
                    }
                    catch (FormatException formatException)
                    {
                        Console.WriteLine("Error parsing lat/lon: " + xpNodeIterator.Current.ToString() + Environment.NewLine + formatException.Message);
                    }
                }

                // Iterate through the ways and write nodes to file
                xpNodeIterator = xpNav.Select("/osm/way");
                while (xpNodeIterator.MoveNext())
                {
                    Console.WriteLine(String.Format("REGIONNAME {0} {1}", regionname, regiondef[0]));
                    newFileWriter.WriteLine(String.Format("REGIONNAME {1}", regionname, regiondef[0]));
                    Console.Write(regiondef[1]);
                    newFileWriter.Write(regiondef[1]);

                    XPathNodeIterator xpWays = xpNodeIterator.Current.SelectChildren("nd", "");
                    while (xpWays.MoveNext())
                    {
                        try
                        {
                            int nodeRef = Int32.Parse(xpWays.Current.GetAttribute("ref", ""));
                            if (nodeDict.ContainsKey(nodeRef))
                            {
                                Console.WriteLine(String.Format("\t{0} {1}", LatitudeDecimalToDMS(nodeDict[nodeRef].Latitude), LongitudeDecimalToDMS(nodeDict[nodeRef].Longitude)));
                                newFileWriter.WriteLine(String.Format("\t{0} {1}",
                                                                      LatitudeDecimalToDMS(nodeDict[nodeRef].Latitude),
                                                                      LongitudeDecimalToDMS(nodeDict[nodeRef].Longitude)));
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Error parsing ways: " + xpWays.Current.ToString() );
                        }
                    }
                    Console.WriteLine();
                    newFileWriter.WriteLine();
                }
            }
            catch (XmlException xmlException)
            {
                Console.WriteLine("XML Error: " + xmlException.ToString());
            }
            catch (ArgumentException argException)
            {
                Console.WriteLine("Argument Error: " + argException.ToString());
            }

            newFileWriter.Flush();
            newFileWriter.Close();
            newFileWriter.Dispose();
        }

        protected static String LatitudeDecimalToDMS(decimal latitudeDecimal)
        {
            String latitudeDMS;
            decimal latitudeM;
            decimal latitudeS;
            int latitudeSRemainder;

            latitudeDMS = "";

            if (latitudeDecimal >= 0)
            {
                latitudeDMS += "N";
            }
            else
            {
                latitudeDecimal = -latitudeDecimal;
                latitudeDMS += "S";
            }

            latitudeM = (latitudeDecimal - (int)latitudeDecimal) * 60;
            latitudeS = (latitudeM - (int)latitudeM) * 60;
            latitudeSRemainder = (int)((latitudeS - (int)latitudeS) * 1000);

            latitudeDMS += String.Format("{0:000}.{1:00}.{2:00}.{3:000}",
                                         (int)latitudeDecimal,
                                         (int)latitudeM,
                                         (int)latitudeS,
                                         latitudeSRemainder);

            return latitudeDMS;
        }

        protected static String LongitudeDecimalToDMS(decimal longitudeDecimal)
        {
            String longitudeDMS;
            decimal longitudeM;
            decimal longitudeS;
            int longitudeSRemainder;

            longitudeDMS = "";

            if (longitudeDecimal >= 0)
            {
                longitudeDMS += "E";
            }
            else
            {
                longitudeDecimal = -longitudeDecimal;
                longitudeDMS += "W";
            }

            longitudeM = (longitudeDecimal - (int)longitudeDecimal) * 60;
            longitudeS = (longitudeM - (int)longitudeM) * 60;
            longitudeSRemainder = (int)((longitudeS - (int)longitudeS) * 1000);


            longitudeDMS += String.Format("{0:000}.{1:00}.{2:00}.{3:000}",
                                         (int)longitudeDecimal,
                                         (int)longitudeM,
                                         (int)longitudeS,
                                         longitudeSRemainder);

            return longitudeDMS;
        }

        protected struct Point
        {
            public decimal Latitude;
            public decimal Longitude;
        }
    }
}
