using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DIPS.Xamarin.UI.Internal.Xaml;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace DIPS.Xamarin.UI.Controls.Toast
{
    /// <summary>
    ///     Toast control that would appear on top of the presented view
    /// </summary>
    public class Toast
    {
        private Toast()
        {
            Application.Current.PageAppearing -= OnPageAppearing;
            Application.Current.PageAppearing += OnPageAppearing;

            Application.Current.PageDisappearing -= OnPageDisappearing;
            Application.Current.PageDisappearing += OnPageDisappearing;
        }

        /// <summary>
        ///     Get the current instance of the Toast control
        /// </summary>
        public static Toast Current { get; } = new Toast();

        private CancellationTokenSource CancellationSource { get; set; } = new CancellationTokenSource();
        private Dictionary<string, Grid> ToastContainers { get; } = new Dictionary<string, Grid>();
        private ToastLayout ToastLayout { get; set; }
        private ToastOptions ToastOptions { get; set; }

        private void OnPageDisappearing(object sender, Page page)
        {
            if (page is ContentPage contentPage)
            {
                CancellationSource.Cancel();
                _ = HideToast(contentPage);
            }
        }

        private void OnPageAppearing(object sender, Page e)
        {
            Initialize();
        }

        /// <summary>
        ///     Set Toast container in Page Content on Page load
        /// </summary>
        internal void Initialize()
        {
            _ = GetToastContainerSettingUpIfNeededAsync();
        }

        /// <summary>
        ///     Displays the Toast control
        /// </summary>
        /// <param name="text">Text to be displayed in the Toast control</param>
        /// <param name="options">An <see cref="Action{ToastOptions}" /> to modify Toast options</param>
        /// <param name="layout">An <see cref="Action{ToastLayout}" /> to modify Toast layout</param>
        /// <returns>A void <c>Task</c></returns>
        public async Task DisplayToast(string text, Action<ToastOptions> options, Action<ToastLayout> layout)
        {
            var toastOptions = new ToastOptions();
            options(toastOptions);
            var layoutOptions = new ToastLayout();
            layout(layoutOptions);

            await DisplayToast(text, toastOptions, layoutOptions);
        }

        /// <summary>
        ///     Displays the Toast control
        /// </summary>
        /// <param name="text">Text to be displayed in the Toast control</param>
        /// <param name="options"><see cref="ToastOptions" /> to set for the Toast control</param>
        /// <param name="layout"><see cref="ToastLayout" /> to set for the Toast control</param>
        /// <returns>A void <c>Task</c></returns>
        public async Task DisplayToast(string text, ToastOptions options = null, ToastLayout layout = null)
        {
            // set properties
            ToastOptions = options ?? new ToastOptions();
            ToastLayout = layout ?? new ToastLayout();

            // get toast container
            var toastContainer = await GetToastContainerSettingUpIfNeededAsync();
            if (toastContainer == null)
            {
                return;
            }

            // toast view
            var toastView = GetToast(text);
            toastContainer.Children.Add(toastView);

            // animate toast
            if (ToastOptions.OnBeforeDisplayingToast != null)
            {
                await ToastOptions.OnBeforeDisplayingToast(toastView);
            }

            // hide toast
            if (ToastOptions.Duration > 0)
            {
                await HideToastIn(ToastOptions.Duration);
            }
        }

        /// <summary>
        ///     Closes the displaying Toast control
        /// </summary>
        /// <returns>A void <c>Task</c></returns>
        public async Task HideToast()
        {
            // get current page
            var currentPage = GetCurrentContentPage();
            if (currentPage == null)
            {
                return;
            }

            await HideToast(currentPage);
        }

        private async Task HideToast(ContentPage currentPage)
        {
            CancellationSource.Cancel();

            // get toast view, can be only one or none
            var toastContainer = FindByName(currentPage.Id.ToString());
            var toastView = toastContainer?.Children.FirstOrDefault(w => w.GetType() == typeof(ToastView));
            if (toastView == null)
            {
                return;
            }

            // animate toast
            if (ToastOptions.OnBeforeHidingToast != null)
            {
                await ToastOptions.OnBeforeHidingToast((ToastView)toastView);
            }

            // remove toast
            toastContainer.Children.Remove(toastView);
        }

        private async Task<Grid?> GetToastContainerSettingUpIfNeededAsync()
        {
            // get current page
            var currentPage = GetCurrentContentPage();
            if (currentPage == null)
            {
                return null;
            }

            // try get toast container
            var toastContainer = FindByName(currentPage.Id.ToString());
            if (toastContainer != null) // found toast container
            {
                // check opened toasts, can be only one or none
                var oldToast = toastContainer.Children.FirstOrDefault(w => w.GetType() == typeof(ToastView));
                if (oldToast != null) // close old toast
                {
                    CancellationSource.Cancel();
                    toastContainer.Children.Remove(oldToast);
                }
            }
            else // no toast container
            {
                // create and register toast container
                toastContainer = new Grid();
                RegisterName(currentPage.Id.ToString(), toastContainer);

                // old content
                var oldContent = currentPage.Content;

                // set new content
                await MainThread.InvokeOnMainThreadAsync(() => { currentPage.Content = toastContainer; });
                toastContainer.Children.Add(oldContent);
            }

            return toastContainer;
        }

        private static ContentPage? GetCurrentContentPage()
        {
            if (Application.Current.MainPage is ContentPage contentPage)
            {
                return contentPage;
            }

            if (Application.Current.MainPage is NavigationPage navigationPage)
            {
                if (navigationPage.CurrentPage.Navigation.ModalStack.Any())
                {
                    return navigationPage.CurrentPage.Navigation.ModalStack.Last() as ContentPage;
                }

                return navigationPage.CurrentPage as ContentPage;
            }

            if (Application.Current.MainPage is TabbedPage tabbedPage)
            {
                if (tabbedPage.CurrentPage is NavigationPage tabNavigationPage)
                {
                    if (tabNavigationPage.CurrentPage.Navigation.ModalStack.Any())
                    {
                        return tabNavigationPage.CurrentPage.Navigation.ModalStack.Last() as ContentPage;
                    }

                    return tabNavigationPage.CurrentPage as ContentPage;
                }

                return tabbedPage.CurrentPage as ContentPage;
            }

            return null;
        }

        private ToastView GetToast(string text)
        {
            var toast = new ToastView
            {
                BackgroundColor = ToastLayout.BackgroundColor,
                CornerRadius = ToastLayout.CornerRadius,
                FontFamily = ToastLayout.FontFamily,
                FontSize = ToastLayout.FontSize,
                HasShadow = ToastLayout.HasShadow,
                LineBreakMode = ToastLayout.LineBreakMode,
                MaxLines = ToastLayout.MaxLines,
                Padding = ToastLayout.Padding,
                Margin = new Thickness(ToastLayout.HorizontalMargin, ToastLayout.PositionY,
                    ToastLayout.HorizontalMargin, 0),
                Text = text,
                TextColor = ToastLayout.TextColor
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                ToastOptions.ToastAction?.Invoke();
            };
            toast.GestureRecognizers.Add(tapGesture);

            return toast;
        }

        private async Task HideToastIn(int timeInMilliseconds)
        {
            CancellationSource.Cancel();
            CancellationSource = new CancellationTokenSource();

            await Task.Delay(timeInMilliseconds, CancellationSource.Token);

            await HideToast();
        }

        private void RegisterName(string name, Grid container)
        {
            if (!ToastContainers.ContainsKey(name))
            {
                ToastContainers[name] = container;
            }
        }

        private Grid? FindByName(string name)
        {
            return ToastContainers.ContainsKey(name) ? ToastContainers[name] : null;
        }
    }
}