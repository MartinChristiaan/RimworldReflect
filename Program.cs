using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using System.Xml.Linq;

namespace RimworldReflect
{
    public static class RimworldReflector
    {
        public static List<Type> defs = new List<Type>();
        public static List<XElement> xmlDefs = new List<XElement>();
        public static List<Assembly> assemblies = new List<Assembly>();
        public static Type DefType;
        public static Type UnsavedAttribute;
        public static BindingFlags Binding = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        public static void LoadDefs(string rimworldPath)
        {
            HashSet<string> assembliesHash = new HashSet<string>();

            Console.WriteLine(rimworldPath);
            var rimworldAssembly = Assembly.LoadFrom(rimworldPath + @"\RimWorldWin_Data\Managed\Assembly-CSharp.dll");
            assemblies.Add(rimworldAssembly);
            var types = rimworldAssembly.GetTypes();
            DefType = types.Where(x => x.Name == "Def").Single();
            UnsavedAttribute = types.Where(x => x.Name == "UnsavedAttribute").Single();

            FindAssemblies(rimworldPath, assemblies, assembliesHash);
            FindDefs(assemblies, defs, DefType);
            LoadXML(rimworldPath);
        }

        private static void LoadXML(string path)
        {
            foreach (var modFolder in new DirectoryInfo(path + @"\Mods").GetDirectories())
            {
                foreach (var subfolder in new DirectoryInfo(modFolder.FullName).GetDirectories())
                {
                    if (subfolder.Name == "Defs" || subfolder.Name == "defs")
                    {
                        foreach (string file in Directory.GetFiles(subfolder.FullName, "*.xml", SearchOption.AllDirectories))
                        {

                            var myfile = XElement.Load(file);
                            myfile.Elements().ToList().ForEach(x => xmlDefs.Add(x));

                        }
                    }
                }
            }
        }

        private static void FindAssemblies(string path, List<Assembly> assemblies, HashSet<string> assembliesHash)
        {
            foreach (var modFolder in new DirectoryInfo(path + @"\Mods").GetDirectories())
            {
                foreach (var subfolder in new DirectoryInfo(modFolder.FullName).GetDirectories())
                {

                    if (subfolder.Name == "About")
                    {
                        //ReadModMetaData(subfolder);
                    }
                    if (subfolder.Name == "Assemblies")
                    {
                        foreach (string file in Directory.GetFiles(subfolder.FullName, "*.dll"))
                        {
                            try
                            {
                                var assembly = Assembly.LoadFrom(file);
                                if (!assembliesHash.Contains(assembly.GetName().Name))
                                {
                                    assembliesHash.Add(assembly.GetName().Name);
                                    assemblies.Add(assembly);
                                }

                            }
                            catch (Exception)
                            {
                                Console.WriteLine(file);
                            }
                        }

                    }
                }
            }
        }

        private static void FindDefs(List<Assembly> assemblies, List<Type> defs, Type defType)
        {
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes().Where(x => defType.IsAssignableFrom(x)))
                {
                    defs.Add(type);
                    //Console.WriteLine(assembly.GetName().Name + " : " + type.Name);
                }
            }
            defs = defs.OrderBy(x => x.Name).ToList();
        }
    }

     class Program
     {
        static HashSet<string> knownTypes = new HashSet<string>();
        static bool IsClass(Type type) 
        {
           
            var t4 = RimworldReflector.DefType.IsAssignableFrom(type);
            var tests = new List<bool>() { t4, !type.IsClass };
            return tests.All(x => x == false);
        }
        static void WriteClass(Type deftype)
        {
            Console.WriteLine(deftype.Name);
            var pythonstring = "";
            pythonstring += "class " + deftype.Name + ":\n";
            pythonstring += "   def __init__(self): \n";
            foreach (var field in deftype.GetFields())
            {
                var fieldtype = field.FieldType;
                if (fieldtype == typeof(string))
                {
                    pythonstring += GetStr(field, "str");
                }
                else if (fieldtype == typeof(float))
                {
                    pythonstring += GetStr(field, "float");
                }
                else if (fieldtype == typeof(bool))
                {
                    pythonstring += GetStr(field, "bool");
                }
                else if (fieldtype == typeof(int))
                {
                    pythonstring += GetStr(field, "int");
                }

                else if (RimworldReflector.DefType.IsAssignableFrom(fieldtype))
                {
                    pythonstring += GetStr(field, fieldtype.Name);
                }
                else if (fieldtype.IsGenericType && fieldtype.GetGenericTypeDefinition().Name == typeof(System.Collections.Generic.List<>).Name)
                {
                    var listtype = fieldtype.GetGenericArguments()[0];
                    pythonstring += GetStr(field, listtype.Name + "_list");
                    //Console.WriteLine("Listype  : " + listtype.Name);
                    if (IsClass(listtype))
                    {
                      //  Console.WriteLine("Listype  : " + listtype.Name + " Is class");
                        if (!knownTypes.Contains(listtype.Name))
                        {
                            knownTypes.Add(listtype.Name);

                            WriteClass(listtype);
                        }
                        
                    }
                }
                else if (fieldtype.IsClass)
                {
                    pythonstring += GetStr(field, fieldtype.Name);
                    if (!knownTypes.Contains(fieldtype.Name))
                    {
                        knownTypes.Add(fieldtype.Name);
                        WriteClass(fieldtype);
                        
                    }
                }
            }
            System.IO.File.WriteAllText(@"C:\Users\marti\source\repos\MyRimworldModXS\DefGenerator\library\" + deftype.Name.ToLower() + ".py",pythonstring);

        }


        
        static void Main(string[] args)
        {
            RimworldReflector.LoadDefs(@"C:\Users\marti\source\repos\MyRimworldModXS");
            var defclass = RimworldReflector.defs.Where(x => x.Name == "HediffDef").Single();
            foreach (var def in RimworldReflector.defs)
            {
                WriteClass(def);
            }
            //WriteClass(defclass);
            // get all XML def names
            var defcollection = new Dictionary<string, List<string>>();
            
            foreach (var xmldef in RimworldReflector.xmlDefs)
            {
            
                var defType = xmldef.Name.ToString();
                var el = xmldef.Element("defName");
                    if (el != null)
                    {
                        string defName = el.Value;
                        if (!defcollection.ContainsKey(defType))
                        {
                            defcollection.Add(defType, new List<string>());
                        }
                        defcollection[defType].Add(defName);
                    }
                
                
            }
            foreach (var kv in defcollection)
            {
                var filename = kv.Key.ToLower() + "_options.py";
                var pythonstring = "";
                foreach (var option in kv.Value)
                {
                    pythonstring += option + " = '" + option + "'\n";

                }
                System.IO.File.WriteAllText(@"C:\Users\marti\source\repos\MyRimworldModXS\DefGenerator\library\" + filename, pythonstring);

            }
            
               
             



            //    // Create class 

            //}


            Console.ReadKey();






        }

        private static string GetStr(FieldInfo field,string postfix)
        {
            return "       self." + field.Name + "_" + postfix  +" = '' \n";
        }
    }
}


