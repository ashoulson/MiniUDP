using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("MiniUDP")]
[assembly: AssemblyDescription("A Simple UDP Layer for Shipping and Receiving Byte Arrays")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Alexander Shoulson")]
[assembly: AssemblyProduct("MiniUDP")]
[assembly: AssemblyCopyright("Copyright © Alexander Shoulson 2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

#if DEBUG
[assembly: InternalsVisibleTo("Tests")]
#endif

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("e978a9ee-3fae-40f9-a785-e65cfc7c41ec")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.0.8.5")]
[assembly: AssemblyFileVersion("0.0.8.5")]
