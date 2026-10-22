using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AirspyPanel
{
    static class InternationalTests
    {
        public static int Run()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            try {
                L.Validate(); var app = new Application(); int renders = 0;
                using(var panel = new PanelApp(true)) {
                    panel.C<TextBox>("Port").Text = "30006";
                    panel.Text("Aircraft", "17");
                    panel.ChangeLanguage("fr-FR", false);
                    if(panel.C<TextBox>("Port").Text != "30006" || panel.C<TextBlock>("Aircraft").Text != "17") throw new Exception("Language reset inputs or statistics");
                    panel.C<TextBox>("Port").Text = "30005";
                    panel.Text("Aircraft", "—");
                    var captured = L.Capture(L.Source("s071") + @"C:\example\设置\map.xml");
                    if(!captured.Render().Contains(@"C:\example\设置\map.xml")) throw new Exception("Translated opaque path parameter");
                    panel.CloseVrs = () => { throw new InvalidOperationException("simulated VRS close failure"); };
                    if(panel.StopAll(false).GetAwaiter().GetResult()) throw new Exception("Failed VRS close reported success");
                    if(!panel.C<Button>("LinkVrs").IsEnabled) throw new Exception("Failed close left operation locked");
                    foreach (string language in L.Languages) foreach (bool dark in new[] { false, true }) foreach(bool compact in new[] { false, true }) {
                        panel.ChangeLanguage(language, false); panel.C<ComboBox>("ThemeChoice").SelectedIndex = dark ? 1 : 0;
                        panel.SetCompact(compact, false);
                        foreach (string page in new[] { "Home", "Settings", "SettingsReceiver", "SettingsPaths", "SettingsAppearance", "Log", "Details" }) {
                            panel.Navigate(page);
                            panel.C<TextBlock>("Footer").Text = L.Get("Demo");
                            panel.C<TextBlock>("Footer").FontSize = 9;
                            panel.C<TextBlock>("Footer").Visibility = Visibility.Visible;
                            foreach (int dpi in new[] { 96, 144, 192, 240 }) {
                                var size = compact ? new Size(304, 570) : new Size(1180, 780);
                                var content = (FrameworkElement)panel.Window.Content;
                                content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
                                var frame = new System.Windows.Threading.DispatcherFrame();
                                content.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
                                System.Windows.Threading.Dispatcher.PushFrame(frame);
                                panel.C<TextBlock>("Footer").Visibility = Visibility.Visible;
                                content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
                                if (panel.Window.ResizeMode != (compact ? ResizeMode.CanMinimize : ResizeMode.CanResize)) throw new Exception("Language changed window mode");
                                if (compact && page == "Home" && panel.C<ScrollViewer>("CompactView").ScrollableHeight > 1) throw new Exception("Compact overflow: " + language);
                                var bitmap = new RenderTargetBitmap((int)(size.Width*dpi/96), (int)(size.Height*dpi/96), dpi,dpi,PixelFormats.Pbgra32); bitmap.Render(content);
                                // Retain representative images; all combinations are rendered.
                                if(dpi == 96 && (page == "Home" || page == "Settings")) {
                                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                                    using(var file=File.Create(Path.Combine(root,"preview-"+language+"-"+(dark?"dark":"light")+"-"+(compact?"compact":"full")+"-"+page+".png"))) encoder.Save(file);
                                }
                                renders++;
                            }
                        }
                    }
                    panel.Navigate("Log"); panel.AddLog(L.Source("s240")); panel.RefreshLogs();
                    panel.ChangeLanguage("en-US",false); if(!panel.ExportLogText().Contains("Settings saved")) throw new Exception("English event rendering");
                    panel.ChangeLanguage("ja-JP",false); if(!panel.ExportLogText().Contains("設定を保存")) throw new Exception("Live event translation");
                }
                File.WriteAllText(Path.Combine(root,"international-test.txt"), "PASS: " + renders + " language/theme/mode/page/DPI renders; resource parity; dynamic event language; fixed compact mode.\n"); return 0;
            } catch(Exception ex) { File.WriteAllText(Path.Combine(root,"international-test.txt"),"FAIL\n"+ex); return 1; }
        }
    }
}
