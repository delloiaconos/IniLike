using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

class CompatibilityProbe
{
    static object call(object instance, string name, Type[] types, params object[] args)
    {
        Type type=instance.GetType();
        MethodInfo method=type.GetMethod(name,types);
        // Older consumer assemblies may still expose PascalCase method names.
        if(method==null)
            method=type.GetMethod(Char.ToUpperInvariant(name[0])+name.Substring(1),types);
        if(method==null) throw new MissingMethodException(type.FullName,name);
        return method.Invoke(instance,args);
    }
    static string get(object instance,string section,string key)
    {return (string)call(instance,"getParameter",new[]{typeof(string),typeof(string),typeof(string)},section,key,"fallback");}
    static List<string> table(object instance,string table)
    {return (List<string>)call(instance,"getTable",new[]{typeof(string)},table);}
    static void require(bool condition,string message){if(!condition)throw new Exception(message);}
    static string snapshot(Type type,string path,bool requireMemoryAdd)
    {
        object cfg=Activator.CreateInstance(type,new object[]{path});
        require(get(cfg,"DATA","name")=="test","section parsing");
        require(get(cfg,"DATA","equals")=="fallback","multiple equals must be ignored by current parser");
        require(get(cfg,"data","name")=="fallback","case sensitivity");
        require(get(cfg,"DATA","punctuation")=="value","trailing delimiter trimming");
        require(get(cfg,"DATA","quoted")=="\"hello\"","quotes are literal");
        require((double)call(cfg,"getParameter",new[]{typeof(string),typeof(string),typeof(double)},"DATA","decimal",0.0)==1.25,"decimal comma");
        require(!(bool)call(cfg,"getParameter",new[]{typeof(string),typeof(string),typeof(bool)},"DATA","boolean",true),"non TRUE boolean");
        require((int)call(cfg,"getParameter",new[]{typeof(string),typeof(string),typeof(int)},"DATA","invalid",42)==42,"invalid number default");
        List<string> rows=table(cfg,"ROWS");require(rows.Count==2&&rows[0]=="a;b;c"&&rows[1]=="d;e", "table rows");
        rows.Add("mutable");require(table(cfg,"ROWS").Count==3,"returned table is live");
        table(cfg,"missing").Add("ignored");require(table(cfg,"missing").Count==0,"missing table detached");
        call(cfg,"addParameter",new[]{typeof(string),typeof(string),typeof(string)},"DATA","added","new");
        string added=get(cfg,"DATA","added");
        if(requireMemoryAdd)
            require(added=="new","addParameter must store defaults in memory");
        else
        {
            require(added=="new"||added=="fallback","unexpected consumer addParameter behavior");
            if(added=="fallback") Console.WriteLine("NOTE: OperatorUI addParameter remains a no-op; IniLike stores defaults in memory.");
        }
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
            require(snapshot(ini,input,true)==snapshot(ui,input,false),"parser behavior differs");
            foreach(Type type in new[]{ini,ui})
            {
                object empty=Activator.CreateInstance(type);

                get(empty,"unexpected-section-file","missing");
                require(!File.Exists("unexpected-section-file"),"default getters must not write files");
                require(!(bool)call(empty,"checkSection",new[]{typeof(string)},"unexpected-section-file"),"default getters must not create sections");
                string duplicate=Path.Combine(folder,"duplicate-"+type.Assembly.GetName().Name+".ini");File.WriteAllText(duplicate,"[S]\nk=1;\nk=2;\n");
                try{Activator.CreateInstance(type,new object[]{duplicate});throw new Exception("Duplicate accepted");}
                catch(TargetInvocationException ex){require(ex.InnerException is ArgumentException,"unexpected duplicate error");}
            }
            Type[] setter={typeof(string),typeof(string),typeof(string)};
            foreach(Type type in new[]{ini,ui})
            {
                require(type.GetMethod("setParameter",setter)!=null||type.GetMethod("SetParameter",setter)!=null,"setParameter missing");
                object current=Activator.CreateInstance(type,new object[]{input});
                call(current,"setParameter",setter,"DATA","name","override");
                call(current,"setParameter",setter,"DATA","added","123");
                call(current,"setParameter",setter,"NEW","key","created");
                require(get(current,"DATA","name")=="override"&&get(current,"NEW","key")=="created","runtime override");
                require((int)call(current,"getParameter",new[]{typeof(string),typeof(string),typeof(int)},"DATA","added",0)==123,"typed override read");
                call(current,"setParameter",setter,"DATA","name"," last=.,; ");
                require(get(current,"DATA","name")==" last=.,; ","last override wins without parsing or trimming");
                require(get(current,"data","name")=="fallback","setter section case sensitivity");
                require(get(current,"DATA","Name")=="fallback","setter key case sensitivity");
                require(File.ReadAllText(input)==text,"INI file changed");
                object empty=Activator.CreateInstance(type);

                call(empty,"setParameter",setter,"runtime-section","key","value");
                require((bool)call(empty,"checkSection",new[]{typeof(string)},"runtime-section"),"setter creates section");
                require(get(empty,"runtime-section","key")=="value","setter on empty instance");
                require(!File.Exists("runtime-section"),"setter must not write with default settings");

                call(current,"setParameter",setter,"runtime-section","key","value");
                require(!File.Exists("runtime-section")&&File.ReadAllText(input)==text,"loaded setter must not write with default settings");
            }
            Console.WriteLine("PASS: IniLike and OperatorUI parsing and setParameter behavior match; overrides do not write files.");
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
