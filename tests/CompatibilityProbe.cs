using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

class CompatibilityProbe
{
    static object Call(object instance, string name, Type[] types, params object[] args)
    {return instance.GetType().GetMethod(name,types).Invoke(instance,args);}
    static string Get(object instance,string section,string key)
    {return (string)Call(instance,"getParameter",new[]{typeof(string),typeof(string),typeof(string)},section,key,"fallback");}
    static List<string> Table(object instance,string table)
    {return (List<string>)Call(instance,"getTable",new[]{typeof(string)},table);}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Snapshot(Type type,string path)
    {
        object cfg=Activator.CreateInstance(type,new object[]{path});
        Require(Get(cfg,"DATA","name")=="test","section parsing");
        Require(Get(cfg,"DATA","equals")=="fallback","multiple equals must be ignored by current parser");
        Require(Get(cfg,"data","name")=="fallback","case sensitivity");
        Require(Get(cfg,"DATA","punctuation")=="value","trailing delimiter trimming");
        Require(Get(cfg,"DATA","quoted")=="\"hello\"","quotes are literal");
        Require((double)Call(cfg,"getParameter",new[]{typeof(string),typeof(string),typeof(double)},"DATA","decimal",0.0)==1.25,"decimal comma");
        Require(!(bool)Call(cfg,"getParameter",new[]{typeof(string),typeof(string),typeof(bool)},"DATA","boolean",true),"non TRUE boolean");
        Require((int)Call(cfg,"getParameter",new[]{typeof(string),typeof(string),typeof(int)},"DATA","invalid",42)==42,"invalid number default");
        List<string> rows=Table(cfg,"ROWS");Require(rows.Count==2&&rows[0]=="a;b;c"&&rows[1]=="d;e", "table rows");
        rows.Add("mutable");Require(Table(cfg,"ROWS").Count==3,"returned table is live");
        Table(cfg,"missing").Add("ignored");Require(Table(cfg,"missing").Count==0,"missing table detached");
        Call(cfg,"addParameter",new[]{typeof(string),typeof(string),typeof(string)},"DATA","added","new");
        Require(Get(cfg,"DATA","added")=="fallback","addParameter is currently a no-op");
        return String.Join("|",rows.ToArray());
    }
    static int Main(string[] args)
    {
        Type ini=Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("ConfigurationFilesReader.ConfigurationFile",true);
        Type ui=Assembly.LoadFrom(Path.GetFullPath(args[1])).GetType("ConfigurationFilesReader.ConfigurationFile",true);
        string original=Environment.CurrentDirectory;
        string folder=Path.Combine(Path.GetTempPath(),"inilike-compat-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            Environment.CurrentDirectory=folder;
            string input=Path.Combine(folder,"config.ini");
            string text="## comment\n[DATA]\nname = test;\nequals = a=b;\npunctuation = value.,;\nquoted = \"hello\";\ndecimal = 1,25;\nboolean = yes;\ninvalid = abc;\n[TABLE:ROWS]\na;b;c;\nd;e;\n";
            File.WriteAllText(input,text);
            Require(Snapshot(ini,input)==Snapshot(ui,input),"parser behavior differs");
            foreach(Type type in new[]{ini,ui})
            {
                object empty=Activator.CreateInstance(type);
                type.GetField("UpdateFile").SetValue(empty,true);
                Get(empty,"unexpected-section-file","missing");
                Require(File.Exists("unexpected-section-file"),"legacy UpdateFile side effect changed");
                File.Delete("unexpected-section-file");
                string duplicate=Path.Combine(folder,"duplicate-"+type.Assembly.GetName().Name+".ini");File.WriteAllText(duplicate,"[S]\nk=1;\nk=2;\n");
                try{Activator.CreateInstance(type,new object[]{duplicate});throw new Exception("Duplicate accepted");}
                catch(TargetInvocationException ex){Require(ex.InnerException is ArgumentException,"unexpected duplicate error");}
            }
            Type[] setter={typeof(string),typeof(string),typeof(string)};
            foreach(Type type in new[]{ini,ui})
            {
                Require(type.GetMethod("SetParameter",setter)!=null,"SetParameter missing");
                object current=Activator.CreateInstance(type,new object[]{input});
                Call(current,"SetParameter",setter,"DATA","name","override");
                Call(current,"SetParameter",setter,"DATA","added","123");
                Call(current,"SetParameter",setter,"NEW","key","created");
                Require(Get(current,"DATA","name")=="override"&&Get(current,"NEW","key")=="created","runtime override");
                Require((int)Call(current,"getParameter",new[]{typeof(string),typeof(string),typeof(int)},"DATA","added",0)==123,"typed override read");
                Call(current,"SetParameter",setter,"DATA","name"," last=.,; ");
                Require(Get(current,"DATA","name")==" last=.,; ","last override wins without parsing or trimming");
                Require(Get(current,"data","name")=="fallback","setter section case sensitivity");
                Require(Get(current,"DATA","Name")=="fallback","setter key case sensitivity");
                Require(File.ReadAllText(input)==text,"INI file changed");
                object empty=Activator.CreateInstance(type);
                type.GetField("UpdateFile").SetValue(empty,true);
                Call(empty,"SetParameter",setter,"runtime-section","key","value");
                Require((bool)Call(empty,"checkSection",new[]{typeof(string)},"runtime-section"),"setter creates section");
                Require(Get(empty,"runtime-section","key")=="value","setter on empty instance");
                Require(!File.Exists("runtime-section"),"setter must not write with UpdateFile enabled");
                type.GetField("UpdateFile").SetValue(current,true);
                Call(current,"SetParameter",setter,"runtime-section","key","value");
                Require(!File.Exists("runtime-section")&&File.ReadAllText(input)==text,"loaded setter must not write with UpdateFile enabled");
            }
            Console.WriteLine("PASS: IniLike and OperatorUI parsing and SetParameter behavior match; overrides do not write files.");
            return 0;
        }
        finally
        {
            Environment.CurrentDirectory=original;
            GC.Collect();GC.WaitForPendingFinalizers();
            Directory.Delete(folder,true);
        }
    }
}
