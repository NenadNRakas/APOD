using AdaptiveCards;
using Newtonsoft.Json.Linq;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Runtime.Serialization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.UserActivities;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Shell;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
namespace APOD
{
    public sealed partial class MainPage : Page
    {
        // Settings name strings, used to preserve UI values between sessions.
        const string SettingUpdateInstall = "1";
        const string SettingSelectedDate = "2022, 06, 19";
        const string SettingDateToday = "date today";
        const string SettingShowOnStartup = "show on startup";
        const string SettingImageCountToday = "image count today";
        const string SettingLimitRange = "limit range";
        // The objective of the NASA API portal is to make NASA data, including imagery, eminently accessible to application developers. 
        const string EndpointURL = "https://api.nasa.gov/planetary/apod";
        // The objective of the NASA API portal is to make NASA data, including imagery, eminently accessible to application developers. 
        const string DesignerURL = "https://aicloudptyltd.business.site";
        private const int imageDownloadLimit = 50;
        // A count of images downloaded today.
        private int imageCountToday;
        // Application settings status
        private string imageAutoLoad = "Yes";
        // Selected date
        private static string selectedDate;
        Object todayObject;
        // Declare a container for the local settings.
        ApplicationDataContainer localSettings;
        // To support the Timeline, we need to record user activity, and create an Adaptive Card.
        // June 16, 1995  : the APOD launch date.
        DateTime launchDate = new DateTime(1995, 6, 16);
        UserActivitySession _currentActivity;
        AdaptiveCard apodTimelineCard;
        private static bool ImageLoaded = false;
        private static bool UpdateInstalling = false;
        private static bool UpdateInAMin = false;
        private ApplicationDataContainer GetLocalSettings()
        {
            return localSettings;
        }
        private void ReadSettings(ApplicationDataContainer localSettings)
        {
            // Installation object status after restart
            Object SUI = localSettings.Values[SettingUpdateInstall];
            if (SUI != null)
            {
                bool InstallUpdateSetting = bool.Parse((string)SUI.ToString());
                if (InstallUpdateSetting.Equals(true)) { UpdateInstalling = true; }
            }
            else { UpdateInstalling = false; }
            // If the app is being started the same day that it was run previously, then the images downloaded today count
            // needs to be set to the stored setting. Otherwise it should be zero.
            bool isToday = false;
            todayObject = localSettings.Values[SettingDateToday];
            if (todayObject != null)
            {
                // First check to see if this is the same day as the previous run of the app.
                DateTime dt = DateTime.Parse((string)todayObject.ToString());
                if (dt.Equals(DateTime.Today))
                {
                    isToday = true;
                }
            }
            // Set the default for images downloaded today.
            imageCountToday = 0;
            if (isToday)
            {
                Object value = localSettings.Values[SettingImageCountToday];
                if (value != null)
                {
                    imageCountToday = int.Parse((string)value);
                }
            }
            ImagesTodayTextBox.Text = imageCountToday.ToString();
            // Set the UI checkboxes, depending on the stored settings or defaults if there are no settings.
            Object showTodayObject = localSettings.Values[SettingShowOnStartup];
            if (showTodayObject != null)
            {
                ShowTodaysImageCheckBox.IsChecked = bool.Parse((string)showTodayObject);
            }
            else
            {
                // Set the default.
                ShowTodaysImageCheckBox.IsChecked = true;
            }
            Object limitRangeObject = localSettings.Values[SettingLimitRange];
            if (limitRangeObject != null)
            {
                LimitRangeCheckBox.IsChecked = bool.Parse((string)limitRangeObject);
            }
            else
            {
                // Set the default.
                LimitRangeCheckBox.IsChecked = false;
            }
            // Show today's image if the check box requires it or restore the state after an update.
            if (localSettings.Values[SettingSelectedDate] == null)
            {
                localSettings.Values[SettingSelectedDate] = SettingSelectedDate;
            }
            DateTime dateTime = DateTime.Parse((string)localSettings.Values[SettingSelectedDate].ToString());
            if (ShowTodaysImageCheckBox.IsChecked == true)
            {
                switch (UpdateInstalling)
                {
                    case false:
                        MonthCalendar.Date = DateTime.Today;
                        break;
                    case true:
                        MonthCalendar.Date = dateTime.Date;
                        break;
                }
            }
            else
            {
                switch (UpdateInstalling)
                {
                    case false:
                        break;
                    case true:
                        MonthCalendar.Date = dateTime.Date;
                        break;
                }
            }
        }
        public MainPage()
        {
            // Create the container for the local settings.
            localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            this.InitializeComponent();
            // Set the maximum date to today, and the minimum date to the date APOD was launched.
            MonthCalendar.MinDate = launchDate;
            MonthCalendar.MaxDate = DateTime.Today;
            // Load saved settings.
            ReadSettings(GetLocalSettings());
            // AdaptiveCards Call.
            SetupForTimelineAsync();
            if (UpdateInstalling) { UpdateInstalling = false; }
            CheckForMandatoryUpdates();
        }
        private async void SetupForTimelineAsync()
        {
            // First create the adaptive card.
            CreateAdaptiveCardForTimeline();
            // Second record the user activity.
            await GenerateActivityAsync();
        }
        private void CreateAdaptiveCardForTimeline()
        {
            // Create an Adaptive Card specifically to reference this app in the Windows 10 Timeline.
            apodTimelineCard = new AdaptiveCard("1.0")
            {
                // Select a good background image.
                //BackgroundImage = new Uri("https://1drv.ms/u/s!AuDUa3nlO2p8go92kjalNUGThk_ojA")
                BackgroundImage = new Uri("https://4.bp.blogspot.com/-0r-rmEv9rvA/T78blbcG7WI/AAAAAAAAEEw/6uAIEhGJ2gM/s1600/Hdhut.blogspot.com+%252813%2529.jpg")
            };
            var apodSpace = new AdaptiveTextBlock
            {
                MaxLines = 0
            };
            apodTimelineCard.Body.Add(apodSpace);
            // Add a heading to the card, allowing the heading to wrap to the next line if necessary.
            var apodHeading = new AdaptiveTextBlock
            {
                Text = "A.i.POD",
                Size = AdaptiveTextSize.Small,
                Weight = AdaptiveTextWeight.Bolder,
                Color = AdaptiveTextColor.Accent,
                Wrap = true,
                MaxLines = 1
            };
            apodTimelineCard.Body.Add(apodHeading);
            // Update and load application settings status
            if (ShowTodaysImageCheckBox.IsChecked == true) { imageAutoLoad = "Yes"; };
            if (ShowTodaysImageCheckBox.IsChecked == false) { imageAutoLoad = "No"; };
            // Add a description to the card, noting it can wrap for several lines.an [@.i.]™ Design
            var apodDesc = new AdaptiveTextBlock
            {
                Text = $"Auto Load: {imageAutoLoad.ToString()}",
                Size = AdaptiveTextSize.Small,
                Weight = AdaptiveTextWeight.Bolder,
                Color = AdaptiveTextColor.Light,
                Wrap = true,
                MaxLines = 1,
                Separator = true
            };
            apodTimelineCard.Body.Add(apodDesc);
            // Add a Counter to the card, noting it can wrap for several lines.
            var apodCount = new AdaptiveTextBlock
            {
                Text = $"Loaded: {imageCountToday} Today.",
                Size = AdaptiveTextSize.Small,
                Weight = AdaptiveTextWeight.Bolder,
                Color = AdaptiveTextColor.Light,
                Wrap = true,
                MaxLines = 1,
                Separator = true
            };
            apodTimelineCard.Body.Add(apodCount);
            // Add a description to the card, noting it can wrap for several lines.
            var apodDes = new AdaptiveTextBlock
            {
                Text = $"Presenting NASA's Astronomy Picture of the Day.",
                Size = AdaptiveTextSize.Small,
                Weight = AdaptiveTextWeight.Default,
                Color = AdaptiveTextColor.Light,
                Wrap = true,
                MaxLines = 1,
                Separator = true
            };
            apodTimelineCard.Body.Add(apodDes);
        }
        private async Task GenerateActivityAsync()
        {
            // Get the default UserActivityChannel and query it for our UserActivity. If the activity doesn't exist, one is created.
            UserActivityChannel channel = UserActivityChannel.GetDefault();
            // The text here should be treated as a title for this activity, and should be unique to this app.
            UserActivity userActivity = await channel.GetOrCreateUserActivityAsync("APOD-UWP");
            // Populate required properties: DisplayText and ActivationUri are required.
            userActivity.VisualElements.DisplayText = "A.i.POD Timeline activities";
            // The name in the ActivationUri must match the name in the protocol setting in the manifest file (except for the "://" part).
            userActivity.ActivationUri = new Uri("aicloud://");
            // Build the Adaptive Card from a JSON string.
            userActivity.VisualElements.Content = AdaptiveCardBuilder.CreateAdaptiveCardFromJson(apodTimelineCard.ToJson());
            // Set the mime type of the user activity, in this case, an application.
            userActivity.ContentType = "application/octet-stream";
            // Save the new metadata.
            await userActivity.SaveAsync();
            // Dispose of any current UserActivitySession and create a new one.
            _currentActivity?.Dispose();
            _currentActivity = userActivity.CreateSession();
        }
        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            // Make sure the full range of dates is available.
            LimitRangeCheckBox.IsChecked = false;
            // This will not load up the image, just sets the calendar to the APOD launch date.
            MonthCalendar.Date = launchDate;
        }
        private void ShowTodaysImageCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            // Update the settings and refresh the cards
            ShowTodaysImageCheckBox.IsChecked = true;
            imageAutoLoad = "Yes";
            SetupForTimelineAsync();
        }
        private void ShowTodaysImageCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            // Update the settings and refresh the cards
            ShowTodaysImageCheckBox.IsChecked = false;
            imageAutoLoad = "No";
            SetupForTimelineAsync();
        }
        private void LimitRangeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Set the calendar minimum date to the first of the current year.
            var firstDayOfThisYear = new DateTime(DateTime.Today.Year, 1, 1);
            MonthCalendar.MinDate = firstDayOfThisYear;
        }
        private void LimitRangeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Set the calendar minimum date to the launch of the APOD program.
            MonthCalendar.MinDate = launchDate;
        }
        private void MonthCalendar_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            // Build the date parameter string for the date selected, or the last date if a range is specified.
            DateTimeOffset dt = (DateTimeOffset)MonthCalendar.Date;
            selectedDate = $"{dt.Year.ToString()}-{dt.Month.ToString("00")}-{dt.Day.ToString("00")}";
            string apiNasa = $"TZ6ay3nXkgGqVPMlWbrxYArpggcdyqSCjR7ZVeim";
            string URLParams = $"?date={selectedDate}&api_key={apiNasa}";
            var client = new HttpClient();
            // Populate the Http client appropriately.
            client.BaseAddress = new Uri(EndpointURL);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // The critical call: sends a GET request with the appropriate parameters.
            HttpResponseMessage response = client.GetAsync(URLParams).Result;
            if (imageCountToday < imageDownloadLimit)
            {
                // Make Duplication clearing
                _ = RetrievePhoto(GetImageCountToday());
            }
            else
            {
                switch (UpdateInstalling)
                {
                    case false:
                        DescriptionTextBox.Text = "We were unable to retrieve the NASA picture for that day. This message is usually " +
                            "caused by exceeding the image download limit of 50 images per day In addition if the following test " +
                            "error code is OK, then everything is in good health and you can continue tomorrow. NotFound and BadRequest " +
                            "would probably mean it's too early and the service is still unavailable for today. Error Code: " + 
                            $"{response.StatusCode.ToString()} {response.ReasonPhrase}";
                        break;
                    case true:
                        if (imageCountToday < imageDownloadLimit)
                        {
                            // Make Duplication clearing
                            _ = RetrievePhoto(GetImageCountToday() - 1);
                        }
                        else
                        {
                            DescriptionTextBox.Text = "We were unable to retrieve the NASA picture for that day. This message is " +
                                "usually caused by exceeding the image download limit of 50 images per day In addition if the " +
                                "following test error code is OK, then everything is in good health and you can continue tomorrow. " +
                                "Error Code: " + $"{response.StatusCode.ToString()} {response.ReasonPhrase}";
                        }
                        break;
                }
            }
        }
        private bool IsSupportedFormat(string photoURL)
        {
            // Extract the extension and force to lower case for comparison purposes.
            string ext = Path.GetExtension(photoURL).ToLower();
            // Check the extension against supported UWP formats.
            return (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" ||
                    ext == ".tif" || ext == ".bmp" || ext == ".ico" || ext == ".svg");
        }
        private int GetImageCountToday()
        {
            return imageCountToday;
        }
        private async Task RetrievePhoto(int imageCount)
        {
            var client = new HttpClient();
            string description = null;
            string photoUrl = null;
            string copyright = null;
            // Set the UI elements to defaults
            ImageCopyrightTextBox.Text = "© " + "NASA";
            DescriptionTextBox.Text = " ";
            // Build the date parameter string for the date selected, or the last date if a range is specified.
            DateTimeOffset dt = (DateTimeOffset)MonthCalendar.Date;
            selectedDate = $"{dt.Year.ToString()}-{dt.Month.ToString("00")}-{dt.Day.ToString("00")}";
            string apiNasa = $"TZ6ay3nXkgGqVPMlWbrxYArpggcdyqSCjR7ZVeim";
            string URLParams = $"?date={selectedDate}&api_key={apiNasa}";
            // Populate the Http client appropriately.
            client.BaseAddress = new Uri(EndpointURL);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // The critical call: sends a GET request with the appropriate parameters.
            HttpResponseMessage response = client.GetAsync(URLParams).Result;
            if (response.IsSuccessStatusCode)
            {
                // Be ready to catch any data/server errors.
                try
                {
                    // Parse response using Newtonsoft APIs.
                    string responseContent = await response.Content.ReadAsStringAsync();
                    // Parse the response string for the details we need.
                    JObject jResult = JObject.Parse(responseContent);
                    // Now get the image.
                    photoUrl = (string)jResult["url"];
                    var photoURI = new Uri(photoUrl);
                    var bmi = new BitmapImage(photoURI);
                    description = (string)jResult["explanation"];
                    copyright = (string)jResult["copyright"];
                    // Set the variable
                    ImagePictureBox.Source = bmi;
                    if (IsSupportedFormat(photoUrl))
                    {
                        // Get the copyright message, but fill with "NASA" if no name is provided.
                        //copyright = (string)jResult["copyright"];
                        if (copyright != null && copyright.Length > 0)
                        {
                            ImageCopyrightTextBox.Text = "© " + copyright;
                        }
                        // Switch the visibility back
                        WebView1.Visibility = Visibility.Collapsed;
                        // Populate the description text box.
                        DescriptionTextBox.Text = description;
                    }
                    else
                    {
                        WebView1.Visibility = Visibility.Visible;
                        WebView1.Navigate(new Uri(photoUrl));
                        ImageCopyrightTextBox.Text = "© " + copyright;
                        DescriptionTextBox.Text = description + $"Url is: {photoUrl}";
                    }
                }
                catch (Exception ex)
                {
                    WebView1.Visibility = Visibility.Visible;
                    WebView1.Navigate(new Uri(photoUrl));
                    if (copyright != null && copyright.Length > 0)
                    {
                        ImageCopyrightTextBox.Text = "© " + copyright;
                    }
                    DescriptionTextBox.Text = description + $" Msg: {ex.Message}";
                }
                switch (UpdateInstalling)
                {
                    case true:
                        // Restore the normal application state.
                        if (UpdateInAMin == false) { GetImageCountToday(); }
                        if (UpdateInAMin == true) { ++imageCountToday; } //GetImageCountToday(); }
                        break;
                    case false:
                        // Keep track of our downloads, in case we reach the limit.
                        ++imageCountToday;
                        break;
                }
                ImageLoaded = true;
                // Refresh the coutn display.
                ImagesTodayTextBox.Text = imageCountToday.ToString();
            }
            else
            {
                DescriptionTextBox.Text = "We were unable to retrieve the NASA picture for that day. Common issue is that it's too " +
                    "early in the day. The other is network problems. Error Code: " +
                    $"{response.StatusCode.ToString()} {response.ReasonPhrase}";
            }
            SetupForTimelineAsync();
        }
        private void Grid_LostFocus(object sender, RoutedEventArgs e)
        {
            WriteSettings();
            SetupForTimelineAsync();
        }
        private void OnSuspending(object sender, SuspendingEventHandler e)
        {
            WriteSettings();
            if (UpdateInstalling) { InstallUpdatesAsync(); }
        }
        private void WriteSettings()
        {
            // Check and update the application settings status
            if (ShowTodaysImageCheckBox.IsChecked == true) { imageAutoLoad = "Yes"; };
            if (ShowTodaysImageCheckBox.IsChecked == false) { imageAutoLoad = "No"; };
            // Preserve the required UI settings in the local storage container.
            switch (UpdateInstalling)
            {
                case true:
                    if (ImageLoaded) { localSettings.Values[SettingSelectedDate] = MonthCalendar.Date.ToString(); }
                    else { localSettings.Values[SettingSelectedDate] = SettingSelectedDate; }  //MonthCalendar.Date.ToString()
                    break;
                case false:
                    localSettings.Values[SettingDateToday] = DateTime.Today.ToString();
                    break;
            }
            localSettings.Values[SettingUpdateInstall] = UpdateInstalling.ToString();
            localSettings.Values[SettingShowOnStartup] = ShowTodaysImageCheckBox.IsChecked.ToString();
            localSettings.Values[SettingLimitRange] = LimitRangeCheckBox.IsChecked.ToString();
            localSettings.Values[SettingImageCountToday] = imageCountToday.ToString();
        }
        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // Bring View to Visible
            WebView1.Visibility = Visibility.Visible;
            // Nevigate to site resource 
            WebView1.Navigate(new Uri(DesignerURL));
            // Add Copyright to TextBox
            ImageCopyrightTextBox.Text = "©  (2018 - Present) " +
                                         "                           " +
                                         "an [@.i.]™ Production " +
                                         "                           " +
                                         "by Nenad Rakas";
            // Add Description to TextBox
            DescriptionTextBox.Text = "Manual: Application is set by default to automatically load the latest presentation of the day " +
                "and count the daily limit of 50, that you can keep track of in the Timeline - which resets every day! Use the Launch " +
                "button to take you back in time when the service first began. You will automatically receive content by selecting a " +
                "desired date in the drop-down calendar menu. By deselecting the show on startup checkbox, you can save an image " +
                "when restarting the application. Hovering over elements will guide you with tooltip popups. Credits: Special thank " +
                "you to Microsoft and NASA.";
        }
        private async void CheckForMandatoryUpdates()
        {
            await Task.Delay(TimeSpan.FromSeconds(63.63));
            UpdateInAMin = false;
            StoreContext updateManager = StoreContext.GetDefault();
            IReadOnlyList<StorePackageUpdate> updates = await updateManager.GetAppAndOptionalStorePackageUpdatesAsync();            
            if (updates.Count > 0)
            {
                foreach (StorePackageUpdate u in updates)
                {
                    if (u.Mandatory) 
                    {
                        DownloadUpdatesAsync();
                    }
                }
            }
        }
        private async void DownloadUpdatesAsync()
        {
            StoreContext updateManager = StoreContext.GetDefault();
            IReadOnlyList<StorePackageUpdate> updates = await updateManager.GetAppAndOptionalStorePackageUpdatesAsync();
            if (updates.Count > 0)
            {
                IAsyncOperationWithProgress<StorePackageUpdateResult, StorePackageUpdateStatus> downloadOperation =
                    updateManager.RequestDownloadStorePackageUpdatesAsync(updates);
                downloadOperation.Progress = async (asyncInfo, progress) =>
                {
                    // Show progress UI
                    await downloadOperation.AsTask();
                };
                StorePackageUpdateResult result = await downloadOperation.AsTask();
                if (result.OverallState == StorePackageUpdateState.Completed)
                {
                    // Update was downloaded, add logic to request install
                    DialogUpdate();
                }
            }
        }
        private async void DialogUpdate()
        {
            UpdateInstalling = true;
            WriteSettings();
            ContentDialog updateDialog = new ContentDialog()
            {
                Title = "Required Updates",
                Content = "Please be patient while it completes the process, it's mostly automated and the only cumbersome " +
                "user requirement could be to decide when will you open the app again. Next application start will open the state " +
                "where you left off (unless you've exceeded the daily image download count of 50), it won't cost you an additional " +
                "image download. Should you choose to deliver it now the application will restart automatically for the required " +
                "installation. Alternatively, you can keep delaying it for a minute until you change your choice to now on the " +
                "following reminder.",
                PrimaryButtonText = "In a min.",
                SecondaryButtonText = "Now!",
                DefaultButton = ContentDialogButton.Primary
            };
            var resultDialog = await updateDialog.ShowAsync();
            if (resultDialog == ContentDialogResult.Primary)
            {
                UpdateInAMin = true;
                await Task.Delay(TimeSpan.FromSeconds(63.63));
                DialogUpdate();
            }
            if (resultDialog == ContentDialogResult.Secondary) { InstallUpdatesAsync(); }
        }
        private async void InstallUpdatesAsync()
        {
            StoreContext updateManager = StoreContext.GetDefault();
            IReadOnlyList<StorePackageUpdate> updates = await updateManager.GetAppAndOptionalStorePackageUpdatesAsync();
            // You can save app state here
            IAsyncOperationWithProgress<StorePackageUpdateResult, StorePackageUpdateStatus> installOperation =
                //updateManager.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
                updateManager.TrySilentDownloadAndInstallStorePackageUpdatesAsync(updates);
            StorePackageUpdateResult result = await installOperation.AsTask();
            // Handle error cases here using StorePackageUpdateResult from above
            if (UpdateInstalling)
            {
                DialogExit();
                await Task.Delay(TimeSpan.FromSeconds(33.33));
                if (result.OverallState == StorePackageUpdateState.Completed)
                {
                    // Close the application
                    //App.Current.Exit();
                    ApplicationReboot();
                }
            }
        }
        private async void DialogExit()
        {
            ContentDialog exitDialog = new ContentDialog()
            {
                Title = "Processing Updates",
                Content = "The application will restart shortly to complete the installation... Should the process be unsuccessful " +
                "due to application being out of focus or minimized, you will get a notification that can help you start up the " +
                "app by clicking on the message or you can do it your own way at your nearest convenience. This version " +
                "carries an in app guided update feature and some small bug fixes. See you in a jiffy. Good bye!"
            };
            var resultDialog = await exitDialog.ShowAsync();
        }
        private async void ApplicationReboot()
        {
            int conversationID = 9813;
            // Attempt restart, with arguments.
            AppRestartFailureReason result = await CoreApplication.RequestRestartAsync("-fastInit -level 1 -foo");
            // Restart request denied, send a toast to tell the user to restart manually.
            if (result == AppRestartFailureReason.NotInForeground || result == AppRestartFailureReason.Other)
            {
                //SendToast("Please manually restart.");
                App.Current.Exit();
                // Requires Microsoft.Toolkit.Uwp.Notifications NuGet package version 7.0 or greater
                new ToastContentBuilder()
                    .AddArgument("action", "viewConversation")
                    .AddArgument("conversationId", conversationID)
                    .AddText("A.i.POD Update Complete!")
                    .AddText("Restart was unsuccessful, using this notification you can start the application manually at your " +
                             "nearest conveniance.")
                    .Show(); // With .NET 6 (or later), your TFM must be net6.0-windows10.0.17763.0 or greater
            }
        }
    }
}