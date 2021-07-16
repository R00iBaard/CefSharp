// Copyright © 2013 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CefSharp.Example;
using CefSharp.Example.Handlers;
using CefSharp.Example.JavascriptBinding;
using CefSharp.Example.ModelBinding;
using CefSharp.Example.PostMessage;
using CefSharp.Wpf.Example.Handlers;
using CefSharp.Wpf.Example.ViewModels;
using CefSharp.Wpf.Experimental.Accessibility;

namespace CefSharp.Wpf.Example.Views
{
    public partial class BrowserTabView : UserControl
    {
        //Store draggable region if we have one - used for hit testing
        private Region region;

        bool usernameDone = false;
        bool loggedIn = false;

        public BrowserTabView()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;

            //browser.BrowserSettings.BackgroundColor = Cef.ColorSetARGB(0, 255, 255, 255);

            //Please remove the comments below to use the Experimental WpfImeKeyboardHandler.
            //browser.WpfKeyboardHandler = new WpfImeKeyboardHandler(browser);

            //Please remove the comments below to specify the color of the CompositionUnderline.
            //var transparent = Colors.Transparent;
            //var black = Colors.Black;
            //ImeHandler.ColorBKCOLOR = Cef.ColorSetARGB(transparent.A, transparent.R, transparent.G, transparent.B);
            //ImeHandler.ColorUNDERLINE = Cef.ColorSetARGB(black.A, black.R, black.G, black.B);

            browser.RequestHandler = new ExampleRequestHandler();

            var bindingOptions = new BindingOptions()
            {
                Binder = BindingOptions.DefaultBinder.Binder,
                MethodInterceptor = new MethodInterceptorLogger() // intercept .net methods calls from js and log it
            };

            //To use the ResolveObject below and bind an object with isAsync:false we must set CefSharpSettings.WcfEnabled = true before
            //the browser is initialized.
#if !NETCOREAPP
            CefSharpSettings.WcfEnabled = true;
#endif

            //If you call CefSharp.BindObjectAsync in javascript and pass in the name of an object which is not yet
            //bound, then ResolveObject will be called, you can then register it
            browser.JavascriptObjectRepository.ResolveObject += (sender, e) =>
            {
                var repo = e.ObjectRepository;

                //When JavascriptObjectRepository.Settings.LegacyBindingEnabled = true
                //This event will be raised with ObjectName == Legacy so you can bind your
                //legacy objects
#if NETCOREAPP
                if (e.ObjectName == "Legacy")
                {
                    repo.Register("boundAsync", new AsyncBoundObject(), options: bindingOptions);
                }
                else
                {
                    if (e.ObjectName == "boundAsync")
                    {
                        repo.Register("boundAsync", new AsyncBoundObject(), options: bindingOptions);
                    }
                    else if (e.ObjectName == "boundAsync2")
                    {
                        repo.Register("boundAsync2", new AsyncBoundObject(), options: bindingOptions);
                    }
                }
#else
                if (e.ObjectName == "Legacy")
                {
                    repo.Register("bound", new BoundObject(), isAsync: false, options: BindingOptions.DefaultBinder);
                    repo.Register("boundAsync", new AsyncBoundObject(), isAsync: true, options: bindingOptions);
                }
                else
                {
                    if (e.ObjectName == "bound")
                    {
                        repo.Register("bound", new BoundObject(), isAsync: false, options: BindingOptions.DefaultBinder);
                    }
                    else if (e.ObjectName == "boundAsync")
                    {
                        repo.Register("boundAsync", new AsyncBoundObject(), isAsync: true, options: bindingOptions);
                    }
                    else if (e.ObjectName == "boundAsync2")
                    {
                        repo.Register("boundAsync2", new AsyncBoundObject(), isAsync: true, options: bindingOptions);
                    }
                }
#endif
            };

            browser.JavascriptObjectRepository.ObjectBoundInJavascript += (sender, e) =>
            {
                var name = e.ObjectName;

                Debug.WriteLine($"Object {e.ObjectName} was bound successfully.");
            };

            browser.DisplayHandler = new DisplayHandler();
            //This LifeSpanHandler implementaion demos hosting a popup in a ChromiumWebBrowser
            //instance, it's still considered Experimental
            //browser.LifeSpanHandler = new ExperimentalLifespanHandler();
            browser.MenuHandler = new MenuHandler();

            //Enable experimental Accessibility support 
            browser.AccessibilityHandler = new AccessibilityHandler(browser);
            browser.IsBrowserInitializedChanged += (sender, args) =>
            {
                if ((bool)args.NewValue)
                {
                    //Uncomment to enable support
                    //browser.GetBrowserHost().SetAccessibilityState(CefState.Enabled);
                }
            };

            var downloadHandler = new DownloadHandler();
            downloadHandler.OnBeforeDownloadFired += OnBeforeDownloadFired;
            downloadHandler.OnDownloadUpdatedFired += OnDownloadUpdatedFired;
            browser.DownloadHandler = downloadHandler;
            browser.AudioHandler = new AudioHandler();

            //Read an embedded bitmap into a memory stream then register it as a resource you can then load custom://cefsharp/images/beach.jpg
            var beachImageStream = new MemoryStream();
            CefSharp.Example.Properties.Resources.beach.Save(beachImageStream, System.Drawing.Imaging.ImageFormat.Jpeg);
            browser.RegisterResourceHandler(CefExample.BaseUrl + "/images/beach.jpg", beachImageStream, Cef.GetMimeType("jpg"));

            var dragHandler = new DragHandler();
            dragHandler.RegionsChanged += OnDragHandlerRegionsChanged;

            browser.DragHandler = dragHandler;
            //browser.ResourceHandlerFactory = new InMemorySchemeAndResourceHandlerFactory();
            //You can specify a custom RequestContext to share settings amount groups of ChromiumWebBrowsers
            //Also this is now the only way to access OnBeforePluginLoad - need to implement IRequestContextHandler
            //browser.RequestContext = new RequestContext(new RequestContextHandler());
            //NOTE - This is very important for this example as the default page will not load otherwise
            //browser.RequestContext.RegisterSchemeHandlerFactory(CefSharpSchemeHandlerFactory.SchemeName, null, new CefSharpSchemeHandlerFactory());
            //browser.RequestContext.RegisterSchemeHandlerFactory("https", "cefsharp.example", new CefSharpSchemeHandlerFactory());

            //You can start setting preferences on a RequestContext that you created straight away, still needs to be called on the CEF UI thread.
            //Cef.UIThreadTaskFactory.StartNew(delegate
            //{
            //    string errorMessage;
            //    //Use this to check that settings preferences are working in your code

            //    var success = browser.RequestContext.SetPreference("webkit.webprefs.minimum_font_size", 24, out errorMessage);
            //});             

            browser.RenderProcessMessageHandler = new RenderProcessMessageHandler();

            browser.LoadError += (sender, args) =>
            {
                // Don't display an error for downloaded files.
                if (args.ErrorCode == CefErrorCode.Aborted)
                {
                    return;
                }

                //Don't display an error for external protocols that we allow the OS to
                //handle in OnProtocolExecution().
                if (args.ErrorCode == CefErrorCode.UnknownUrlScheme && args.Frame.Url.StartsWith("mailto"))
                {
                    return;
                }

                // Display a load error message.
                var errorBody = string.Format("<html><body bgcolor=\"white\"><h2>Failed to load URL {0} with error {1} ({2}).</h2></body></html>",
                                              args.FailedUrl, args.ErrorText, args.ErrorCode);

                args.Frame.LoadHtml(errorBody, base64Encode: true);
            };

            browser.FrameLoadEnd += Browser_FrameLoadEnd;
            browser.LoadingStateChanged += Browser_LoadingStateChanged;
            browser.AddressChanged += Browser_AddressChanged;

            CefExample.RegisterTestResources(browser);

            browser.JavascriptMessageReceived += OnBrowserJavascriptMessageReceived;
        }

        private void Browser_AddressChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue.ToString().ToLower().Contains("secure.sarsefiling.co.za/app/dashboard/individual"))
            {
                loggedIn = true;

                var script = @"
                    document.getElementsByClassName('top-nav-buttons')[3].click();
                ";
                browser.ExecuteScriptAsyncWhenPageLoaded(script);
                Thread.Sleep(1000);
                browser.ExecuteScriptAsync(script);
            }

            if (e.NewValue.ToString().ToLower().Contains("secure.sarsefiling.co.za/app/efdotnet/efdotnet") &&
                e.OldValue.ToString().ToLower().Contains("secure.sarsefiling.co.za/app/dashboard/individual"))
            {
                var script = @"
                    document.getElementsByClassName('mat-list-item-content')[0].click();
                ";
                browser.ExecuteScriptAsyncWhenPageLoaded(script);
                Thread.Sleep(1000);
                browser.ExecuteScriptAsync(script);

                script = @"
                    document.getElementsByClassName('mat-list-item-content')[3].click();
                ";
                browser.ExecuteScriptAsyncWhenPageLoaded(script);
                Thread.Sleep(3000);
                browser.ExecuteScriptAsync(script);
                Thread.Sleep(3000);

                script = @"
                    document.getElementById('anchorFIA').click();
                ";
                browser.ExecuteScriptAsyncWhenPageLoaded(script);
                Thread.Sleep(1000);
                browser.ExecuteScriptAsync(script);
            }
        }

        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            //if (browser.CanExecuteJavascriptInMainFrame && usernameDone && loggedIn)
            //{
            //    browser.EvaluateScriptAsync("document.getElementsByClassName('top-nav-buttons');[0].click();");
            //}
            //if(homeLoaded)
            //{
            //    var script = @"
            //        document.getElementsByClassName('top-nav-buttons')[3].click();
            //    ";
            //    browser.ExecuteScriptAsyncWhenPageLoaded(script);

            //    //browser.EvaluateScriptAsync("document.getElementsByClassName('top-nav-buttons');[0].click();");
            //    //homeLoaded = false;
            //}

            if (browser.CanExecuteJavascriptInMainFrame && usernameDone && !loggedIn)
            {
                browser.EvaluateScriptAsync("document.getElementById('password').click();");
                browser.EvaluateScriptAsync("document.getElementById('password').focused=true");
                browser.ExecuteScriptAsync("document.getElementById('password').value=" + '\'' + "FillMeIn!" + '\'');
                browser.EvaluateScriptAsync("document.getElementById('password').click();");

                Thread.Sleep(100);

                KeyEvent k = new KeyEvent
                {
                    WindowsKeyCode = 68,
                    FocusOnEditableField = true,
                    IsSystemKey = true,
                    Type = KeyEventType.Char
                };

                browser.GetBrowser().GetHost().SendKeyEvent(k);
                Thread.Sleep(100);

                k = new KeyEvent
                {
                    WindowsKeyCode = 8,
                    FocusOnEditableField = true,
                    IsSystemKey = true,
                    Type = KeyEventType.KeyDown
                };
                browser.GetBrowser().GetHost().SendKeyEvent(k);
                Thread.Sleep(100);

                k = new KeyEvent
                {
                    WindowsKeyCode = 127,
                    FocusOnEditableField = true,
                    IsSystemKey = true,
                    Type = KeyEventType.Char
                };
                browser.GetBrowser().GetHost().SendKeyEvent(k);
                Thread.Sleep(100);

                var script = @"
                    document.getElementById('btnLogin').click();
                ";
                browser.ExecuteScriptAsyncWhenPageLoaded(script);
            }
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            //var poo = Convert.ToChar('D');

            //var one = KeyBoardUtilities.GetCharFromKey(Key.NumPad1, false);
            //var _one = KeyBoardUtilities.GetCharFromKey(Key.D1, false);
            //var d = KeyBoardUtilities.GetCharFromKey(Key.D, false);
            //var _d = KeyBoardUtilities.GetCharFromKey(Key.D, true);

            //if (homeLoaded)
            //{
            //    var script = @"
            //        document.getElementsByClassName('top-nav-buttons')[3].click();
            //    ";
            //    browser.ExecuteScriptAsyncWhenPageLoaded(script);
            //}

            if (e.Url.ToLower().Contains("secure.sarsefiling.co.za/app/login"))
            {
                //browser.EvaluateScriptAsync("document.getElementById('username').click();");
                //browser.ExecuteScriptAsync("document.getElementById('username').value=" + '\'' + "davidroux4214" + '\'');

                browser.EvaluateScriptAsync("document.getElementById('username').click();");
                browser.EvaluateScriptAsync("document.getElementById('username').focused=true");
                browser.ExecuteScriptAsync("document.getElementById('username').value=" + '\'' + "davidroux4214" + '\'');
                browser.EvaluateScriptAsync("document.getElementById('username').click();");

                Thread.Sleep(100);

                KeyEvent k = new KeyEvent
                {
                    WindowsKeyCode = 68,
                    FocusOnEditableField = true,
                    IsSystemKey = true,
                    Type = KeyEventType.Char
                };

                browser.GetBrowser().GetHost().SendKeyEvent(k);
                Thread.Sleep(100);

                k = new KeyEvent
                {
                    WindowsKeyCode = 8,
                    FocusOnEditableField = true,
                    IsSystemKey = true,
                    Type = KeyEventType.KeyDown
                };
                browser.GetBrowser().GetHost().SendKeyEvent(k);
                Thread.Sleep(100);

                k = new KeyEvent
                {
                    WindowsKeyCode = 127,
                    FocusOnEditableField = true,
                    IsSystemKey = true,
                    Type = KeyEventType.Char
                };
                browser.GetBrowser().GetHost().SendKeyEvent(k);

                //k = new KeyEvent
                //{
                //    WindowsKeyCode = 13, // enter
                //    FocusOnEditableField = true,
                //    IsSystemKey = true,
                //    Type = KeyEventType.Char
                //};

                //browser.GetBrowser().GetHost().SendKeyEvent(k);

                var script = @"
                    document.getElementById('btnLogin').click();
                ";
                browser.ExecuteScriptAsyncWhenPageLoaded(script);

                usernameDone = true;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //TODO: Ideally we'd be able to bind this directly without having to use codebehind
            var viewModel = e.NewValue as BrowserTabViewModel;

            if (viewModel != null)
            {

                browser.JavascriptObjectRepository.Settings.LegacyBindingEnabled = viewModel.LegacyBindingEnabled;
            }
        }

        private void OnBrowserJavascriptMessageReceived(object sender, JavascriptMessageReceivedEventArgs e)
        {
            //Complext objects are initially expresses as IDicionary (in reality it's an ExpandoObject so you can use dynamic)
            if (typeof(System.Dynamic.ExpandoObject).IsAssignableFrom(e.Message.GetType()))
            {
                //You can use dynamic to access properties
                //dynamic msg = e.Message;
                //Alternatively you can use the built in Model Binder to convert to a custom model
                var msg = e.ConvertMessageTo<PostMessageExample>();

                if (msg.Type == "Update")
                {
                    var callback = msg.Callback;
                    var type = msg.Type;
                    var property = msg.Data.Property;

                    callback.ExecuteAsync(type);
                }
            }
            else if (e.Message is int)
            {
                e.Frame.ExecuteJavaScriptAsync("PostMessageIntTestCallback(" + (int)e.Message + ")");
            }

        }

        private void OnBeforeDownloadFired(object sender, DownloadItem e)
        {
            this.UpdateDownloadAction("OnBeforeDownload", e);
        }

        private void OnDownloadUpdatedFired(object sender, DownloadItem e)
        {
            this.UpdateDownloadAction("OnDownloadUpdated", e);
        }

        private void UpdateDownloadAction(string downloadAction, DownloadItem downloadItem)
        {
            this.Dispatcher.InvokeAsync(() =>
            {
                var viewModel = (BrowserTabViewModel)this.DataContext;
                viewModel.LastDownloadAction = downloadAction;
                viewModel.DownloadItem = downloadItem;
            });
        }

        private void OnBrowserMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(browser);

            if (region.IsVisible((float)point.X, (float)point.Y))
            {
                var window = Window.GetWindow(this);
                window.DragMove();

                e.Handled = true;
            }
        }

        private void OnDragHandlerRegionsChanged(Region region)
        {
            if (region != null)
            {
                //Only wire up event handler once
                if (this.region == null)
                {
                    browser.PreviewMouseLeftButtonDown += OnBrowserMouseLeftButtonDown;
                }

                this.region = region;
            }
        }

        private void OnTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.SelectAll();
        }

        private void OnTextBoxGotMouseCapture(object sender, MouseEventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.SelectAll();
        }

    }
}
