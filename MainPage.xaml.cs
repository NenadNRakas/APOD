using AdaptiveCards;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Runtime.Serialization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.UserActivities;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Shell;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409
namespace APOD
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Settings name strings, used to preserve UI values between sessions.
        const string SettingDateToday = "date today";
        const string SettingShowOnStartup = "show on startup";
        const string SettingImageCountToday = "image count today";
        const string SettingLimitRange = "limit range";
        // Declare a container for the local settings.
        ApplicationDataContainer localSettings;
        // The objective of the NASA API portal is to make NASA data, including imagery, eminently accessible to application developers. 
        const string EndpointURL = "https://api.nasa.gov/planetary/apod";
        // The objective of the NASA API portal is to make NASA data, including imagery, eminently accessible to application developers. 
        const string DesignerURL = "https://aicloudptyltd.business.site";
        // June 16, 1995  : the APOD launch date.
        DateTime launchDate = new DateTime(1995, 6, 16);
        // A count of images downloaded today.
        private int imageCountToday;
        // Application settings status
        private string imageAutoLoad = "Yes";
        // To support the Timeline, we need to record user activity, and create an Adaptive Card.
        UserActivitySession _currentActivity;
        AdaptiveCard apodTimelineCard;
        private void ReadSettings()
        {
            // If the app is being started the same day that it was run previously, then the images downloaded today count
            // needs to be set to the stored setting. Otherwise it should be zero.
            bool isToday = false;
            Object todayObject = localSettings.Values[SettingDateToday];
            if (todayObject != null)
            {
                // First check to see if this is the same day as the previous run of the app.
                DateTime dt = DateTime.Parse((string)todayObject);
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
            // Show today's image if the check box requires it.
            if (ShowTodaysImageCheckBox.IsChecked == true)
            {
                MonthCalendar.Date = DateTime.Today;
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
            ReadSettings();
            // AdaptiveCards Call.
            SetupForTimelineAsync();
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
            userActivity.VisualElements.DisplayText = "[@.i.]™ POD Timeline activities";
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
            // Make Duplication clearing
            _ = RetrievePhoto();
        }
        private bool IsSupportedFormat(string photoURL)
        {
            // Extract the extension and force to lower case for comparison purposes.
            string ext = Path.GetExtension(photoURL).ToLower();
            // Check the extension against supported UWP formats.
            return (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" ||
                    ext == ".tif" || ext == ".bmp" || ext == ".ico" || ext == ".svg");
        }
        private async Task RetrievePhoto()
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
            string dateSelected = $"{dt.Year.ToString()}-{dt.Month.ToString("00")}-{dt.Day.ToString("00")}";
            string apiNasa = $"TZ6ay3nXkgGqVPMlWbrxYArpggcdyqSCjR7ZVeim";
            string URLParams = $"?date={dateSelected}&api_key={apiNasa}";
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
                // Keep track of our downloads, in case we reach the limit.
                ++imageCountToday;
                ImagesTodayTextBox.Text = imageCountToday.ToString();
            }
            else
            {
                DescriptionTextBox.Text = "We were unable to retrieve the NASA picture for that day: " +
                    $"{response.StatusCode.ToString()} {response.ReasonPhrase}";
            }
            SetupForTimelineAsync();
        }
        private void Grid_LostFocus(object sender, RoutedEventArgs e)
        {
            WriteSettings();
            SetupForTimelineAsync();
        }
        private void WriteSettings()
        {
            // Check and update the application settings status
            if (ShowTodaysImageCheckBox.IsChecked == true) { imageAutoLoad = "Yes"; };
            if (ShowTodaysImageCheckBox.IsChecked == false) { imageAutoLoad = "No"; };
            // Preserve the required UI settings in the local storage container.
            localSettings.Values[SettingDateToday] = DateTime.Today.ToString();
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
            DescriptionTextBox.Text = "Manual: Application is set by default to automatically load the latest presentation of the day and count the " +
                                      "daily limit of 50, that you can keep track of in the Timeline - which resets everyday! Use the Launch button " +
                                      "to take you back in time when the service first began. You will automatically receive an image by selecting a " +
                                      "different date in the drop down calendar menu. By deselecting the show on start up checkbox, you can save an " +
                                      "image when restarting the application. Hovering over elements will guide you with tooltip popups. " +
                                      "Credits: Special thank you to Microsoft and NASA.";
        }
    }
}