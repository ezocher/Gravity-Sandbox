﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Graphics.Display;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Shapes;
using System.Threading.Tasks;
using System.Diagnostics;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GravitySandboxUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        GravitySim sim;
        ThreadPoolTimer frameTimer;
        CoreDispatcher dispatcher;
        Random rand;
        bool simRunning;
        bool firstRun;

        const double FrameRate = 60.0;                  // Frames per second in UI time            
        const double StepInterval = 1.0 / 10.0;         // Portion of one UI second of simulation to do per click of Step button
                                                        //

        private static bool frameInProgress = false;
        private static long framesRendered = 0;
        private static long framesDropped = 0;
        private static long totalFrameDelay = 0;

        public static bool appSuspended = false;       // Keep the simulation running (if any), but stop updating the UI while app is suspended
        public static bool scenarioEnding = false;      // True while switching scenarios, stop rendering if truw

        public static bool trailsEnabled = false;

        public MainPage()
        {
            this.InitializeComponent();
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            sim = new GravitySim(backgroundCanvas, this, dispatcher);
            rand = new Random();
            simRunning = false;
            SetRunPauseButton(!simRunning);
            firstRun = true;
            frameInProgress = false;
            Application.Current.Suspending += new SuspendingEventHandler(App_Suspending);
            Application.Current.Resuming += new EventHandler<Object>(App_Resuming);

            // The inital Scenario is loaded by BackgroundGrid_SizeChanged(), which is fired when the app's window size is set initially

            DebugTimerProperties();
        }

        // When the app is suspended keep the simulation calculations running but stop updating the UI
        //   All UI code inside the simulation loop needs to check appSuspended and not run if it's true
        //   e.g. if (appSuspended) return;

        // App will be suspened when it's being quit by the user
        void App_Suspending(Object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            appSuspended = true;
            Debug.WriteLine(">>> App Suspended");
            SetMessageText(">>> App Suspended");   // Use this message to test Suspending. Since Visual Studio debugging prevents suspending, run without
                                                   //   debugging and look for this message to verify that suspend/resume has occured
        }

        private void App_Resuming(Object sender, Object e)
        {
            appSuspended = false;
            Debug.WriteLine("<<< App Resumed");
            AppendMessageText("<<< App Resumed");   // Use this message to test Suspending. Since Visual Studio debugging prevents suspending, run without
                                                    //   debugging and look for this message to verify that suspend/resume has occured
        }

        // Stop UI updates while app is suspended or changing scenarios
        //     This should be checked before any UI updates are marshalled on to the UI thread
        public bool UI_UpdatesStopped()
        {
            return ((appSuspended) || (scenarioEnding));   
        }

        public static void DebugTimerProperties()
        {
            // Display the timer frequency and resolution.
            if (Stopwatch.IsHighResolution)
            {
                Debug.WriteLine("Operations timed using the system's high-resolution performance counter.");
            }
            else
            {
                Debug.WriteLine("Operations timed using the DateTime class.");
            }

            long frequency = Stopwatch.Frequency;
            Debug.WriteLine("  Timer frequency in ticks per second = {0}", frequency);
            Debug.WriteLine("  Timer frequency in ticks per millisecond = {0}", frequency / 1000L);
        }


        /// <summary>
        /// Invoked when this page is about to be displayed in ...
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private void BackgroundGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            /*
            if (e.OriginalSource == backgroundGrid)        // Ignore the tap if it was routed from another control
            {
                sim.Step(defaultStepInterval);
            }
             * */
        }

        //  Updated to do all calculations in this worker thread and to do UI updates that happen inside of this on the UI thread
        public void RunSimFrame(ThreadPoolTimer tpt)
        {
            const double tick = 1.0 / FrameRate; // UI seconds
            const int reportingInterval = 10 * (int)FrameRate;


            // Added check to see if the previous frame is still calculating/rendering when this method gets called by the timer
            // Sufficiently large scenarios (size varies depending on the PC) can take longer than a frame tick to run
            //  When this happens a frame is dropped and we wait until the next tick to check again

            // Waiting an entire frame also gives the XAML/UWP rendering tasks time to finish before they are fired again
            // The amount of rendering work is proportional to the amount of calculation work, so this all works out

            if (frameInProgress)
            {
                framesDropped++;
            }
            else
            {
                frameInProgress = true;
                sim.Step(tick, simRunning);
                frameInProgress = false;
                framesRendered++;

                if ((framesRendered % reportingInterval) == 0)
                    Debug.WriteLine("Frames rendered = {0}, dropped = {1}, dropped pct = {2:F2}", framesRendered, framesDropped,
                        100.0 * (float)framesDropped / (float)(framesRendered + framesDropped));
            }   
        }

        // Updated to be marshalled onto the UI thread
        public void UpdateMonitoredValues(Body body, double simElapsedTime)
        {
            if (UI_UpdatesStopped()) return;   // Stop UI updates while app is suspended or changing scenarios

            var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var velocity = new SimPoint(body.Velocity.X * sim.simSpace.VelocityConnversionFactor,
                    body.Velocity.Y * sim.simSpace.VelocityConnversionFactor);
                velocityTextBlock.Text = "velocity: " + FormatPointToString(velocity) + 
                    String.Format(", v = {0:N1} {1}", Hypotenuse(velocity), sim.simSpace.VelocityUnitsAbbr);
                positionTextBlock.Text = "position: " + FormatPointToString(body.Position) +
                    String.Format(", r = {0:N1} {1}", Hypotenuse(body.Position) - sim.simSpace.DistanceOffset,
                    sim.simSpace.DistanceUnitsAbbr);
                timeTextBlock.Text = String.Format("time: {0:N1} {1}", simElapsedTime, sim.simSpace.TimeUnitsAbbr);
            });
        }

        // Must be called from the UI thread
        public void SetMessageText(string message)
        {
            messageTextBlock.Text = message;
        }

        // Must be called from the UI thread
        public void AppendMessageText(string message)
        {
            const string threeSpaces = "   ";

            messageTextBlock.Text += threeSpaces + message;
        }

        static string FormatPointToString(SimPoint p)
        {
            return String.Format("x = {0:N1}, y = {1:N1}", p.X, p.Y);
        }

        public static double Hypotenuse(SimPoint simPoint)
        {
            return Math.Sqrt((simPoint.X * simPoint.X) + (simPoint.Y * simPoint.Y));
        }

        private void BackgroundGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewSizeChanged();

            // We have to initialize the starting scenario here since we need the initial layout to occur before loading the 
            //   starting scenario
            if (firstRun)
            {
                firstRun = false;
                Button_Click_Scenario1(null, null);
            }
        }

        // Window size changed or zoom level changed
        private void ViewSizeChanged()
        {
            // Pause the running simulation
            if (simRunning)
                frameTimer.Cancel();

            sim.renderer.SetSimulationTransform(backgroundCanvas.ActualWidth, backgroundCanvas.ActualHeight);

            if (!firstRun)
                sim.TransformChanged();

            // Restart the running simulation
            if (simRunning)
                frameTimer = ThreadPoolTimer.CreatePeriodicTimer(RunSimFrame, new TimeSpan(0, 0, 0, 0, 1000 / (int)FrameRate));
        }

        private void ScenarioChanging()
        {
            scenarioEnding = true;
            if (frameTimer != null) frameTimer.Cancel(); // If previous simulation is still running, prevent new frames from being started
            simRunning = false;
            SetRunPauseButton(true);
            framesRendered = framesDropped = totalFrameDelay = 0L;
            frameInProgress = false;

            //Stopwatch s = new Stopwatch(); s.Start();  // For confirming that the Delay().Wait() works as expected

            // Wait 2 simulation ticks for any frames in progress to finish
            Task.Delay(2 * (1000 / (int)FrameRate)).Wait();

            //Debug.WriteLine("Scenario changing, waited for {0} ms", s.ElapsedMilliseconds);   // For confirming that the Delay().Wait() works as expected
            scenarioEnding = false;
        }

        #region Load Scenario Buttons
        private void Button_Click_Scenario1(object sender, RoutedEventArgs e)
        {
            ScenarioChanging();
            BuiltInScenarios.LoadNineBodiesScenario(sim);
        }

        private void Button_Click_Scenario2(object sender, RoutedEventArgs e)
        {
            ScenarioChanging();
            //BuiltInScenarios.LoadXRandomBodies(sim, 300, SimRenderer.ColorScheme.AllColors);
            BuiltInScenarios.LoadFiveBodiesScenario(sim, false);
        }

        private void Button_Click_Scenario3(object sender, RoutedEventArgs e)
        {
            ScenarioChanging();
            //BuiltInScenarios.LoadXBodiesCircularCluster(sim, 500, 6.0, SimRenderer.ColorScheme.PastelColors, GravitySim.BodyStartPosition.RandomUniformDensityCircularCluster);
            //BuiltInScenarios.LoadOrbitingBodiesScenario(sim);
            BuiltInScenarios.LoadFiveBodiesScenario(sim, true);
        }

        private void Button_Click_Scenario4(object sender, RoutedEventArgs e)
        {
            ScenarioChanging();
            BuiltInScenarios.LoadLowEarthOrbit(sim);
            //BuiltInScenarios.LoadXBodiesCircularCluster(sim, 400, 2.0, SimRenderer.ColorScheme.GrayColors, GravitySim.BodyStartPosition.RandomDenseCenterCircularCluster);
            //BuiltInScenarios.LoadFourBodiesScenario(sim);
        }
        #endregion


        #region Run/Pause and Step Buttons
        private void stepButton_Click(object sender, RoutedEventArgs e)
        {
            sim.Step(StepInterval, simRunning);
        }

        private void SetRunPauseButton(bool setToRun)
        {
            if (setToRun)
                runPauseButton.Content = "Run";
            else
                runPauseButton.Content = "Pause";
            stepButton.IsEnabled = setToRun;      // Step is available when Run is available
        }

        // Try logging calculation details to a text file and then analyze in Excel.
        //  
        // Use Show trails to turn logging on and off. 
        //
        // Accumulate values in memory from a Run click to a Pause click and then dump them to a text file.

        private void runPauseButton_Click(object sender, RoutedEventArgs e)
        {
            SetRunPauseButton(simRunning);

            if (simRunning)
            {
                // Pause button clicked
                frameTimer.Cancel();

                if (DumpData.collectingData)
                {
                    DumpData.collectingData = false;
                    DumpData.DumpAccumulatedData(sim);
                }
            }
            else
            {
                if (DumpData.loggingOn)
                {
                    DumpData.collectingData = true;
                    DumpData.BeginAccumulatingData(sim);
                }

                // Run button clicked
                frameTimer = ThreadPoolTimer.CreatePeriodicTimer(RunSimFrame, new TimeSpan(0, 0, 0, 0, 1000 / (int)FrameRate));
            }
            simRunning = !simRunning;
        }

        private void enableTrailsCheckBox_Click(object sender, RoutedEventArgs e)
        {
            trailsEnabled = !trailsEnabled;

            DumpData.loggingOn = trailsEnabled;
            if (DumpData.loggingOn)
                AppendMessageText(">> Logging enabled, Run starts recording, then Pause to dump");
            else
                SetMessageText("Running " + GravitySim.currentScenarioName);
        }
        #endregion


       #region Zoom and Faster/Slower Buttons
        private void zoomMinusButton_Click(object sender, RoutedEventArgs e)
        {
            sim.ZoomMinus();
            ViewSizeChanged();
        }

        private void zoomPlusButton_Click(object sender, RoutedEventArgs e)
        {
            sim.ZoomPlus();
            ViewSizeChanged();
        }

        private void timeSlowerButton_Click(object sender, RoutedEventArgs e)
        {
            sim.RunSlower();
        }

        private void timeFasterButton_Click(object sender, RoutedEventArgs e)
        {
            sim.RunFaster();
        }
        #endregion


        #region Tests
        private void testCoordinateMapping()
        {
            TranslateTransform t = new TranslateTransform();

            Body a = new Body(new SimPoint(0, 0), sim.simSpace);
            t = sim.renderer.CircleTransform(a);
            Body b = new Body(new SimPoint(-500, 0), sim.simSpace);
            t = sim.renderer.CircleTransform(b);
            Body c = new Body(new SimPoint(500, 0), sim.simSpace);
            t = sim.renderer.CircleTransform(c);
            Body d = new Body(new SimPoint(0, 500), sim.simSpace);
            t = sim.renderer.CircleTransform(d);
            Body e = new Body(new SimPoint(0, -500), sim.simSpace);
            t = sim.renderer.CircleTransform(e);
        }

        #endregion

     }
}
