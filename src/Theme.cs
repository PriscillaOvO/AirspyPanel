using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
namespace AirspyPanel {
 static class ThemeManager {
  public static string Effective(string choice) {
   if(choice != "System") return choice == "Dark" ? "Dark" : "Light";
   try { using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
    return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1)) == 0 ? "Dark" : "Light";
   } catch(Exception) { return "Light"; }
  }
  public static void Apply(Window window,string choice) {
   bool dark=Effective(choice)=="Dark";
   Set(window,"0879BD",dark ? "#6EBBE5" : "#0879BD");
   Set(window,"143F5C",dark ? "#F0F8FF" : "#143F5C");
   Set(window,"17689A",dark ? "#8CD1FF" : "#17689A");
   Set(window,"215A80",dark ? "#BCE6FF" : "#215A80");
   Set(window,"234B67",dark ? "#D9E8F4" : "#234B67");
   Set(window,"2489C0",dark ? "#68B9E8" : "#2489C0");
   Set(window,"253E50",dark ? "#DBE8F2" : "#253E50");
   Set(window,"284A62",dark ? "#D5E4F0" : "#284A62");
   Set(window,"286B95",dark ? "#8ED0F6" : "#286B95");
   Set(window,"294A60",dark ? "#C7DCEC" : "#294A60");
   Set(window,"315925",dark ? "#E2F6D0" : "#315925");
   Set(window,"32842C",dark ? "#97D878" : "#32842C");
   Set(window,"3297D2",dark ? "#61B8E8" : "#3297D2");
   Set(window,"348CC0",dark ? "#84CAEF" : "#348CC0");
   Set(window,"385E75",dark ? "#D0E5F4" : "#385E75");
   Set(window,"387F33",dark ? "#97D878" : "#387F33");
   Set(window,"3D5B70",dark ? "#C9DAE8" : "#3D5B70");
   Set(window,"4098C6",dark ? "#75BDE6" : "#4098C6");
   Set(window,"427F9F",dark ? "#75ACCC" : "#427F9F");
   Set(window,"45677E",dark ? "#C0D5E5" : "#45677E");
   Set(window,"47677C",dark ? "#BAD1E2" : "#47677C");
   Set(window,"496C85",dark ? "#BFD4E5" : "#496C85");
   Set(window,"526F83",dark ? "#AFC3D3" : "#526F83");
   Set(window,"527E99",dark ? "#B2D1E6" : "#527E99");
   Set(window,"53768C",dark ? "#B3CDE0" : "#53768C");
   Set(window,"537F98",dark ? "#AACBDE" : "#537F98");
   Set(window,"57AA29",dark ? "#57AA29" : "#57AA29");
   Set(window,"647681",dark ? "#94A9B8" : "#647681");
   Set(window,"66879D",dark ? "#9BBACF" : "#66879D");
   Set(window,"6ACB39",dark ? "#6ACB39" : "#6ACB39");
   Set(window,"6E9BB9",dark ? "#668DA8" : "#6E9BB9");
   Set(window,"7C9B91",dark ? "#4F7569" : "#7C9B91");
   Set(window,"81999A",dark ? "#748F9F" : "#81999A");
   Set(window,"8598A4",dark ? "#8598A4" : "#8598A4");
   Set(window,"8AAA69",dark ? "#638653" : "#8AAA69");
   Set(window,"8ECE55",dark ? "#8ECE55" : "#8ECE55");
   Set(window,"91D158",dark ? "#91D158" : "#91D158");
   Set(window,"94B3C7",dark ? "#526C81" : "#94B3C7");
   Set(window,"9BBED3",dark ? "#456B88" : "#9BBED3");
   Set(window,"A0B9CA",dark ? "#54718A" : "#A0B9CA");
   Set(window,"A6C1D2",dark ? "#455D72" : "#A6C1D2");
   Set(window,"A7C4D5",dark ? "#48667F" : "#A7C4D5");
   Set(window,"ACC6D7",dark ? "#3D586D" : "#ACC6D7");
   Set(window,"AEC8D9",dark ? "#415D74" : "#AEC8D9");
   Set(window,"AFFFFFFF",dark ? "#55233747" : "#AFFFFFFF");
   Set(window,"B0BCC1",dark ? "#B0BCC1" : "#B0BCC1");
   Set(window,"B4CCDC",dark ? "#3E596D" : "#B4CCDC");
   Set(window,"B9DA83",dark ? "#284923" : "#B9DA83");
   Set(window,"BDDDF1",dark ? "#1C344C" : "#BDDDF1");
   Set(window,"C2DFEE",dark ? "#3A5870" : "#C2DFEE");
   Set(window,"C7F69B",dark ? "#C7F69B" : "#C7F69B");
   Set(window,"CCFFFFFF",dark ? "#244B6478" : "#CCFFFFFF");
   Set(window,"CDDEE9",dark ? "#3E5467" : "#CDDEE9");
   Set(window,"CFFFFFFF",dark ? "#5534483D" : "#CFFFFFFF");
   Set(window,"D1E2EC",dark ? "#3A5063" : "#D1E2EC");
   Set(window,"D5EAF5",dark ? "#203449" : "#D5EAF5");
   Set(window,"D5EBFA",dark ? "#355E7D" : "#D5EBFA");
   Set(window,"D5ECFA",dark ? "#2C465F" : "#D5ECFA");
   Set(window,"D6EDAC",dark ? "#365B2E" : "#D6EDAC");
   Set(window,"E1F4BB",dark ? "#416C34" : "#E1F4BB");
   Set(window,"E3EDE7",dark ? "#172B25" : "#E3EDE7");
   Set(window,"E6F5FD",dark ? "#29465E" : "#E6F5FD");
   Set(window,"E6F6FF",dark ? "#314D64" : "#E6F6FF");
   Set(window,"E79B46",dark ? "#E79B46" : "#E79B46");
   Set(window,"EAF4FA",dark ? "#14212D" : "#EAF4FA");
   Set(window,"EDF7FC",dark ? "#263B4D" : "#EDF7FC");
   Set(window,"F1F7FB",dark ? "#1D2E3D" : "#F1F7FB");
   Set(window,"F2F8FC",dark ? "#213445" : "#F2F8FC");
   Set(window,"F2FAFF",dark ? "#324B60" : "#F2FAFF");
   Set(window,"F5FFE8",dark ? "#3F6337" : "#F5FFE8");
   Set(window,"F9FDFF",dark ? "#3C5870" : "#F9FDFF");
   Set(window,"FFFFFF",dark ? "#182633" : "#FFFFFF");
  }
  static void Set(Window window,string key,string hex) {
   var color=(Color)ColorConverter.ConvertFromString(hex);
   window.Resources["Color"+key]=color;
   window.Resources["Brush"+key]=new SolidColorBrush(color);
  }
 }
}
