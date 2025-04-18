using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using Svg;
using System.Drawing.Drawing2D;
using System.IO;

namespace plottrBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        PlottrBMP myPlot;          //the object from the custom Plottr class
        SVGPlottr svgPlot;
        RetentionImg retentionImage;
        string[] comArray;      //array for names of available COM ports
        SerialPort port;        //USB COM port object
        double scaleToPreview;
        int countCmdSent;
        Line selectedPreviewLine;
        //enum GUIStates { T0blank, T1imgLoaded, T2imgSliced, T3usbConnected, T4imgLoadedUsbConnected, T5imgSlicedUsbConnected, T6drawing, T7svgLoaded, T8svgLoadedUsbConnected, T9svgDrawing };
        //enum GUITransitions { H0imgOpen, H1imgSlice, H2imgClear, H3usbOpen, H4usbClose, H5startDrawing, H6pause, H7svgMove, H8svgOpen };
        enum GUIStates { S0blank, S1bmpLoaded, S2bmpSliced, S3usbConnected, S4bmpLoadedUsbConnected, S5bmpSlicedUsbConnected, S6bmpDrawing, S7svgLoaded, S8svgLoadedUsbConnected, S9svgDrawing };
        enum GUIActions { A0bmpOpen, A1bmpSlice, A2clear, A3svgOpen, A4usbOpen, A5startDrawing, A6usbClose };

        GUIStates currentState;
        GUIActions currentTransition;
        //enum imgType { bmp, svg };
        //imgType loadedImgType;

        private readonly Dictionary<(GUIStates, GUIActions), GUIStates> stateTransitions;

        private Dictionary<GUIStates, Action> guiStateActions;


        public MainWindow()
        {
            InitializeComponent();

            //tabControlOptions.Height = canvasPreview.Height + 25 + 2;

            Plottr.RobotWidth = Properties.Settings.Default.RobotWidth;
            Plottr.RobotHeight = Properties.Settings.Default.RobotHeight;
            txtRWidth.Text = Properties.Settings.Default.RobotWidth.ToString();
            txtRHeight.Text = Properties.Settings.Default.RobotHeight.ToString();

            //scaleToPreview = (double)canvasPreview.Width / (double)Plottr.RobotWidth;        //(double)previewWidth / robotWidth;     //used to scale all actual sizes to be shown on screen
            //canvasPreview.Height = Plottr.RobotHeight * scaleToPreview;
            calcCanvasPreviewScale();

            port = new SerialPort();        //creates a blank serial port to be specified later

            selectedPreviewLine = new Line();

            stateTransitions = InitializeStateTransitions();
            InitializeGuiStateActions();

            currentState = GUIStates.S0blank;
            updateGUIelements(); // Update GUI based on initial state

            Plottr.StartGCODE = "G1 Z1\n";
            Plottr.EndGCODE = txtEndGcode.Text + "\n";


            retentionImage = null;
        }

        private void btnUpdateDpi_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtDpi.Text, out int newDpi))
            {
                if (myPlot != null)
                {
                    // Save the image with the new DPI to a MemoryStream
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        SaveBitmapWithDpi(myPlot.TempImg, memoryStream, newDpi, newDpi);
                        memoryStream.Position = 0;

                        // Reload the image with the new DPI from the MemoryStream
                        myPlot = new PlottrBMP(memoryStream);
                        placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, myPlot, false);
                        //MessageBox.Show($"DPI updated to {newDpi}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Invalid DPI value. Please enter a number.");
            }
        }

        private void SaveBitmapWithDpi(Bitmap bitmap, MemoryStream memoryStream, int dpiX, int dpiY)
        {
            using (Bitmap newBitmap = new Bitmap(bitmap))
            {
                newBitmap.SetResolution(dpiX, dpiY);
                newBitmap.Save(memoryStream, ImageFormat.Bmp);
            }
        }

        //canvasPreview.Children.Clear();     //removes previous images/elements from the canvas
        //canvasPreview.Background = System.Windows.Media.Brushes.White;

        private void btnSelectImg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Image file (*.bmp) | *.bmp|Vector file (*.svg) | *.svg";
                if ((bool)openFileDialog.ShowDialog())
                {
                    clearEverything(); // Clears canvas, sets myPlot/svgPlot to null

                    if (openFileDialog.FileName.EndsWith(".bmp")) // loaded .bmp image
                    {
                        currentTransition = GUIActions.A0bmpOpen; // SET TRANSITION FIRST

                        Plottr.Filename = openFileDialog.FileName;
                        myPlot = new PlottrBMP(Plottr.Filename); // Creates object

                        // Center or use retention position
                        if (retentionImage == null)
                        {
                            Plottr.ImgMoveX = Convert.ToInt32((Plottr.RobotWidth - myPlot.GetImgWidth) / 2);
                            Plottr.ImgMoveY = Convert.ToInt32((Plottr.RobotHeight - myPlot.GetImgHeight) / 2);
                        }
                        else
                        {
                            Plottr.ImgMoveX = retentionImage.ImgMoveX;
                            Plottr.ImgMoveY = retentionImage.ImgMoveY;
                        }
                        txtDpi.Text = myPlot.TempImg.HorizontalResolution.ToString(); // Update DPI display
                    }
                    else if (openFileDialog.FileName.EndsWith(".svg")) // loaded .svg image
                    {
                        currentTransition = GUIActions.A3svgOpen; // SET TRANSITION FIRST

                        Plottr.Filename = openFileDialog.FileName;
                        svgPlot = new SVGPlottr(Plottr.Filename); // Creates object

                        // Center or use retention position
                        if (retentionImage == null)
                        {
                            Plottr.ImgMoveX = Convert.ToInt32((Plottr.RobotWidth - svgPlot.GetImgWidth) / 2);
                            Plottr.ImgMoveY = Convert.ToInt32((Plottr.RobotHeight - svgPlot.GetImgHeight) / 2);
                        }
                        else
                        {
                            Plottr.ImgMoveX = retentionImage.ImgMoveX;
                            Plottr.ImgMoveY = retentionImage.ImgMoveY;
                        }
                        // No DPI for SVG
                        txtDpi.Text = "";
                    }
                    else
                    {
                        throw new Exception("Not supported file type");
                    }

                    // Common logic after loading either type
                    handleGUIstates(); // UPDATE STATE MACHINE *AFTER* LOADING
                    placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY); // Place image on canvas
                }
            }
            catch (Exception ex)
            {
                string msg = "Error loading image: " + ex.Message;
                MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Optionally transition back to a known safe state
                // currentTransition = GUIActions.A2clear;
                // handleGUIstates();
            }
        }

        private void btnHoldImg_Click(object sender, RoutedEventArgs e)
        {
            //TODO add button to hold/release image. applies coordinates to new image, can change coordinates along with new image
            //release also removes the image. gcode is relevant for last loaded image

            if (btnHoldImg.Content.ToString().Contains("Hold"))
            {
                retentionImage = new RetentionImg();
                retentionImage.FileName = Plottr.Filename;
                retentionImage.ImgMoveX = Plottr.ImgMoveX;
                retentionImage.ImgMoveY = Plottr.ImgMoveY;
                
                btnHoldImg.Content = "Release first image";
            }
            else if (btnHoldImg.Content.ToString().Contains("Release"))
            {
                retentionImage = null;
                canvasPreview.Children.Clear();     //removes previous images/elements from the canvas
                canvasPreview.Background = System.Windows.Media.Brushes.White;
                if(Plottr.Filename.EndsWith(".bmp"))
                    placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, myPlot, false);
                else if (Plottr.Filename.EndsWith(".svg"))
                    placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, myPlot, false);
                btnHoldImg.Content = "Hold image";
            }

        }

        //private void loadSVGPreviewPoints()
        //{
        //    foreach (PointF point in svgPlot.PreviewPoints)
        //    {
        //        Ellipse currentDot = new Ellipse();
        //        currentDot.Margin = new Thickness(point.X * scaleToPreview, point.Y * scaleToPreview, 0, 0);
        //        currentDot.Fill = System.Windows.Media.Brushes.DarkBlue;
        //        currentDot.Width = 2;
        //        currentDot.Height = 2;
        //        canvasPreview.Children.Add(currentDot);
        //    }
        //}

        private async void btnSliceImg_Click(object sender, RoutedEventArgs e)
        {
            if (myPlot == null || myPlot.Img == null)
            {
                MessageBox.Show("Please load a BMP image before slicing.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            countCmdSent = 0; // Reset command counter for new slice

            try
            {
                // Freeze the image for cross-thread access
                if (myPlot.Img.CanFreeze)
                {
                    myPlot.Img.Freeze();
                }
                else
                {
                    Console.WriteLine("Warning: Could not freeze bitmap image for slicing.");
                }

                // Show loading indicator if needed
                // progressIndicator.Visibility = Visibility.Visible;

                // Perform slicing and preview update
                await previewBMPSlice(myPlot); // Contains Task.Run and Dispatcher.Invoke

                // Hide loading indicator
                // progressIndicator.Visibility = Visibility.Collapsed;

                // Check if slicing was successful
                if (myPlot.GeneratedGCODE == null || !myPlot.GeneratedGCODE.Any())
                {
                    MessageBox.Show("Image slicing failed or produced no GCODE.", "Slicing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return; // Don't transition if slicing failed
                }

                // Update slider
                sliderCmdCount.Maximum = myPlot.AllLines.Count > 0 ? myPlot.AllLines.Count - 1 : 0;
                sliderCmdCount.Value = 0; // Reset slider

                // Update output text
                txtOut.Text = "GCODE commands = " + myPlot.GeneratedGCODE.Count + "\nNumber of lines = " + myPlot.AllLines.Count + "\n";

                // Set the transition and update the state *AFTER* slicing completes
                currentTransition = GUIActions.A1bmpSlice;
                handleGUIstates();

                SystemSounds.Beep.Play(); // Feedback
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during slicing: {ex.Message}", "Slicing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Hide loading indicator if shown
                // progressIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task previewBMPSlice(PlottrBMP o)
        {
            await Task.Run(() => o.GenerateGCODE());       //generates the GCODE to send to the robot

            Dispatcher.Invoke(() =>
            {
                canvasPreview.Children.Clear();
                if (retentionImage != null)
                    placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, retentionImage, true);       //re add retentionImage

                canvasPreview.Background = System.Windows.Media.Brushes.White;
                //draws preview lines that the robot is going to move along:
                foreach (TraceLine lineCommand in o.AllLines)
                {
                    Line myLine = new Line();
                    if (lineCommand.Draw)
                        myLine.Stroke = System.Windows.Media.Brushes.Black;
                    else
                        myLine.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 230, 230, 230));
                    myLine.StrokeThickness = 1;
                    myLine.X1 = lineCommand.X0 * scaleToPreview;
                    myLine.Y1 = lineCommand.Y0 * scaleToPreview;
                    myLine.X2 = lineCommand.X1 * scaleToPreview;
                    myLine.Y2 = lineCommand.Y1 * scaleToPreview;
                    canvasPreview.Children.Add(myLine);
                }
            });
        }

        private async void btnSendImg_Click(object sender, RoutedEventArgs e)
        {
            if (port == null || !port.IsOpen)
            {
                MessageBox.Show("USB port is not connected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            List<string> gcodeToSend = null;
            bool isBmp = false;
            bool isSvg = false;
            GUIStates targetDrawingState = GUIStates.S0blank; // Invalid initial state
            GUIStates returnState = GUIStates.S0blank; // State to return to after drawing

            // Determine GCODE source and target states based on current state
            if ((currentState == GUIStates.S5bmpSlicedUsbConnected || currentState == GUIStates.S6bmpDrawing) && myPlot?.GeneratedGCODE != null && myPlot.GeneratedGCODE.Any())
            {
                gcodeToSend = myPlot.GeneratedGCODE;
                isBmp = true;
                targetDrawingState = GUIStates.S6bmpDrawing;
                returnState = GUIStates.S5bmpSlicedUsbConnected;
            }
            else if ((currentState == GUIStates.S8svgLoadedUsbConnected || currentState == GUIStates.S9svgDrawing) && svgPlot?.GeneratedGCODE != null && svgPlot.GeneratedGCODE.Any())
            {
                gcodeToSend = svgPlot.GeneratedGCODE;
                isSvg = true;
                targetDrawingState = GUIStates.S9svgDrawing;
                returnState = GUIStates.S8svgLoadedUsbConnected;
            }

            if (gcodeToSend == null)
            {
                MessageBox.Show("No valid GCODE available to send. Please load and process an image.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // --- Enter Drawing State ---
            if (currentState != targetDrawingState) // Only transition if not already drawing
            {
                currentTransition = GUIActions.A5startDrawing;
                handleGUIstates(); // Enter S6 or S9 state
                                   // Short delay might be needed if state change has async UI updates
                await Task.Delay(50);
            }

            // Disable potentially problematic controls during send loop (optional, state machine might cover this)
            // btnSelectImg.IsEnabled = false; btnSliceImg.IsEnabled = false;

            bool drawingCompletedSuccessfully = true;
            bool wasPaused = false;

            try
            {
                // Send initial speed command only if starting from the beginning (countCmdSent == 0)
                if (countCmdSent == 0)
                {
                    bool timedOutSpeed = await sendSerialStringAsync(isBmp ? "M220 S150\n" : "M220 S50\n"); // Example speeds
                    if (timedOutSpeed) throw new TimeoutException("Timeout setting initial speed.");
                }

                txtOut.AppendText($"Drawing {(isBmp ? "BMP" : "SVG")} image. Starting/Resuming at command {countCmdSent + 1} of {gcodeToSend.Count}\n");

                // --- GCODE Sending Loop ---
                int loopCounter = countCmdSent; // Use local counter for the loop
                for (; loopCounter < gcodeToSend.Count; loopCounter++)
                {
                    // Check for pause request (by checking button text)
                    if (btnPauseDrawing.Content.ToString().Contains("Continue"))
                    {
                        txtOut.AppendText($"Drawing paused at command {loopCounter}.\n"); // Paused BEFORE sending this command
                        drawingCompletedSuccessfully = false;
                        wasPaused = true;
                        countCmdSent = loopCounter; // Store the next command index to resume from
                        break; // Exit the loop
                    }

                    // Update slider if drawing BMP
                    if (isBmp)
                    {
                        string[] getLineNo = gcodeToSend[loopCounter].Split('L');
                        if (getLineNo.Length > 1 && int.TryParse(getLineNo.Last(), out int lineNo))
                        {
                            Dispatcher.Invoke(() => {
                                if (lineNo >= 0 && lineNo <= sliderCmdCount.Maximum) { sliderCmdCount.Value = lineNo; }
                            });
                        }
                    }

                    // Send the command
                    bool timedOutCmd = await sendSerialStringAsync(gcodeToSend[loopCounter]);
                    if (timedOutCmd)
                    {
                        throw new TimeoutException($"Timeout sending command {loopCounter + 1}: {gcodeToSend[loopCounter].Trim()}");
                    }

                    // Update the main command counter *after* successful send
                    // countCmdSent = loopCounter + 1; // Point to the *next* command index (Changed logic: update after loop or on pause)

                    // Optional delay
                    // await Task.Delay(5);
                } // --- End GCODE Sending Loop ---


                // --- Handle Loop Completion ---
                if (loopCounter == gcodeToSend.Count && !wasPaused) // Loop finished naturally
                {
                    txtOut.AppendText($"Finished sending {loopCounter} commands.\n");
                    countCmdSent = 0; // Reset for next run
                    drawingCompletedSuccessfully = true;
                    // Optional: Send final commands
                    // await sendSerialStringAsync("G1 Z1\n"); // Pen up
                    // await sendSerialStringAsync("G28\n"); // Home
                }
                else if (wasPaused)
                {
                    // loopCounter holds the index of the command *not* sent due to pause
                    countCmdSent = loopCounter; // Ensure countCmdSent points to the paused command
                }
                else
                {
                    // Should not happen unless loop breaks unexpectedly
                    countCmdSent = loopCounter;
                    drawingCompletedSuccessfully = false;
                }

            }
            catch (Exception ex)
            {
                drawingCompletedSuccessfully = false;
                countCmdSent = Math.Max(0, countCmdSent); // Ensure it's not negative on error
                string msg = $"Error during drawing at command {countCmdSent + 1}.\nCommands sent: {countCmdSent}\nError: {ex.Message}";
                MessageBox.Show(msg, "Drawing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                try { await sendSerialStringAsync("G1 Z1\n"); } catch { /* Ignore error trying to lift pen */ } // Attempt safe stop
            }
            finally
            {
                // --- Exit Drawing State ---
                // Only transition back if the loop wasn't paused (pause button handles its own state)
                // Or if an error occurred
                if (!wasPaused || !drawingCompletedSuccessfully)
                {
                    currentTransition = GUIActions.A5startDrawing; // Use A5 to signify "drawing ended/interrupted"
                    handleGUIstates(); // Exit S6/S9 state -> back to S5/S8 (or relevant error state if defined)
                }

                // Re-enable manually disabled controls if any
                // btnSelectImg.IsEnabled = true;
            }
        }
        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (port == null || !port.IsOpen)
            {
                MessageBox.Show("USB port is not connected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtSerialCmd.Text))
            {
                return; // Don't send empty commands
            }

            string commandToSend = txtSerialCmd.Text.Trim() + "\n";
            txtOut.AppendText($">> {commandToSend.Trim()}\n"); // Log command being sent

            bool timedOut = await sendSerialStringAsync(commandToSend);

            if (timedOut)
            {
                txtOut.AppendText("Timeout sending manual command.\n");
            }
            else
            {
                // Response is logged within sendSerialStringAsync now
                // txtOut.AppendText($"<< Response received (or OK)\n");
            }
            txtSerialCmd.Clear(); // Clear input box after sending
            txtSerialCmd.Focus(); // Set focus back to input box
        }

        private void txtSerialCmd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                btnSend_Click(sender, e);
                e.Handled = true; // Prevent further processing of the return key (like beeping)
            }
        }

        private async Task<bool> sendSerialStringAsync(string message)
        {
            bool timedOut = false;
            if (port == null || !port.IsOpen)
            {
                txtOut.Dispatcher.Invoke(() => txtOut.AppendText("Error: Port not open. Cannot send command.\n"));
                return true; // Indicate failure/timeout scenario
            }

            try
            {
                // Consider clearing buffers depending on device behavior
                // port.DiscardInBuffer();
                // port.DiscardOutBuffer();

                // Write the command
                await port.BaseStream.WriteAsync(Encoding.ASCII.GetBytes(message), 0, message.Length);
                await port.BaseStream.FlushAsync(); // Ensure data is sent

                // Log sent message (optional, can be verbose)
                // Console.WriteLine($"Sent: {message.Trim()}");

                // --- Wait for response ("ok" or "GO") ---
                // Use a configurable timeout (e.g., 60 seconds for potentially long moves)
                // Shorter timeout for simple commands? Needs context.
                double timeoutMilliseconds = 60000; // 60 seconds - Adjust as needed!
                var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMilliseconds));
                string lineBuffer = "";

                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = await port.BaseStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

                        if (bytesRead > 0)
                        {
                            string incoming = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            lineBuffer += incoming;
                            // Log raw incoming data to UI (ensure thread safety)
                            txtOut.Dispatcher.Invoke(() => txtOut.AppendText(incoming));

                            // Process complete lines
                            int lineEndIndex;
                            while ((lineEndIndex = lineBuffer.IndexOf('\n')) != -1)
                            {
                                string line = lineBuffer.Substring(0, lineEndIndex).Trim(); // Includes trimming \r
                                lineBuffer = lineBuffer.Substring(lineEndIndex + 1);

                                // Check for expected success response
                                if (line.Equals("ok", StringComparison.OrdinalIgnoreCase) || line.Equals("GO", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Console.WriteLine($"Received expected response: {line}");
                                    return false; // Success, not timed out
                                }

                                // Check for known error responses
                                if (line.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                                {
                                    txtOut.Dispatcher.Invoke(() => txtOut.AppendText($"DEVICE ERROR: {line}\n"));
                                    // Consider this a failure, maybe return true or throw specific exception
                                    // return true;
                                }
                                // Log other lines if needed
                                // else if (!string.IsNullOrWhiteSpace(line)) { Console.WriteLine($"Other response: {line}"); }
                            }
                        }
                        else
                        {
                            // ReadAsync returning 0 usually means end of stream/closed port
                            throw new IOException("Serial port closed unexpectedly while waiting for response.");
                        }
                    }
                }
                catch (OperationCanceledException) // Catches cancellation from CancellationTokenSource (timeout)
                {
                    timedOut = true;
                    txtOut.Dispatcher.Invoke(() => txtOut.AppendText($"Timeout waiting for 'ok'/'GO' after sending: {message.Trim()}\n"));
                }


                // If loop/try finishes without returning false, it means timeout or cancellation
                return timedOut;

            }
            catch (TimeoutException tex) // Catch specific write/read timeouts if configured on port
            {
                txtOut.Dispatcher.Invoke(() => txtOut.AppendText($"Serial Timeout Exception: {tex.Message}\n"));
                return true; // Indicate timeout
            }
            catch (IOException ioex) // Catch port closed errors, etc.
            {
                txtOut.Dispatcher.Invoke(() => txtOut.AppendText($"Serial IO Exception: {ioex.Message}. Port may be closed.\n"));
                // Attempt to close port and update state if an IO error occurs
                if (port != null && port.IsOpen) { try { port.Close(); } catch { } }
                Dispatcher.Invoke(() => {
                    currentTransition = GUIActions.A6usbClose;
                    handleGUIstates();
                });
                return true; // Indicate failure
            }
            catch (InvalidOperationException ioex) // Catch errors like port not open
            {
                txtOut.Dispatcher.Invoke(() => txtOut.AppendText($"Serial Operation Exception: {ioex.Message}. Port may not be open.\n"));
                if (port != null && port.IsOpen) { try { port.Close(); } catch { } }
                Dispatcher.Invoke(() => {
                    currentTransition = GUIActions.A6usbClose;
                    handleGUIstates();
                });
                return true; // Indicate failure
            }
            catch (Exception ex) // Catch other unexpected errors
            {
                txtOut.Dispatcher.Invoke(() => txtOut.AppendText($"Serial Communication Error: {ex.Message}\n"));
                return true; // Indicate failure
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (port != null && port.IsOpen) // Disconnecting
                {
                    port.Close();
                    currentTransition = GUIActions.A6usbClose; // Set transition cause
                    handleGUIstates(); // Update state machine AFTER action
                }
                else // Connecting
                {
                    // Validate COM port selection
                    if (comboBoxCOM.SelectedIndex == -1 || comArray == null || comArray.Length == 0 || comboBoxCOM.SelectedItem.ToString() == "No COM ports found")
                    {
                        MessageBox.Show("Please select an available COM port from the dropdown before connecting.", "USB Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Verify selected port still exists
                    string selectedPortName = comArray[comboBoxCOM.SelectedIndex];
                    string[] currentPorts = SerialPort.GetPortNames();
                    if (!currentPorts.Contains(selectedPortName))
                    {
                        MessageBox.Show($"Selected COM port '{selectedPortName}' is no longer available. Please refresh the list.", "USB Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        comboBoxCOM.ItemsSource = currentPorts.Length > 0 ? currentPorts : new string[] { "No COM ports found" };
                        comboBoxCOM.SelectedIndex = -1;
                        comArray = currentPorts; // Update internal array
                        return;
                    }

                    // Configure and open port
                    port.PortName = selectedPortName;
                    port.BaudRate = 9600;
                    port.Parity = Parity.None;
                    port.DataBits = 8;
                    port.StopBits = StopBits.One;
                    // Consider adding timeouts
                    // port.ReadTimeout = 2000; // 2 seconds
                    // port.WriteTimeout = 2000;
                    port.Open();

                    Thread.Sleep(100); // Brief pause for device initialization

                    currentTransition = GUIActions.A4usbOpen; // Set transition cause
                    handleGUIstates(); // Update state machine AFTER action
                }
            }
            catch (UnauthorizedAccessException uaEx)
            {
                MessageBox.Show($"Access denied to COM port '{port?.PortName}'. It might be in use by another application.\n\n{uaEx.Message}", "USB Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (port != null && port.IsOpen) port.Close(); // Ensure closed
                currentTransition = GUIActions.A6usbClose; // Ensure state reflects closed port
                handleGUIstates();
            }
            catch (IOException ioEx)
            {
                MessageBox.Show($"IO Error connecting to COM port '{port?.PortName}'. Check device connection and drivers.\n\n{ioEx.Message}", "USB Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (port != null && port.IsOpen) port.Close(); // Ensure closed
                currentTransition = GUIActions.A6usbClose; // Ensure state reflects closed port
                handleGUIstates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect/disconnect USB.\n{ex.Message}", "USB Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                if (port != null && port.IsOpen) port.Close(); // Ensure closed
                currentTransition = GUIActions.A6usbClose; // Ensure state reflects closed port
                handleGUIstates();
            }
        }


        private void comboBoxCOM_DropDownOpened(object sender, EventArgs e)
        {
            string previouslySelected = comboBoxCOM.SelectedItem as string;
            comArray = SerialPort.GetPortNames();
            if (comArray.Length == 0)
            {
                comboBoxCOM.ItemsSource = new string[] { "No COM ports found" };
                comboBoxCOM.SelectedIndex = 0; // Show the message
            }
            else
            {
                comboBoxCOM.ItemsSource = comArray;
                // Try to re-select the previously selected port if it still exists
                if (previouslySelected != null && comArray.Contains(previouslySelected))
                {
                    comboBoxCOM.SelectedItem = previouslySelected;
                }
                else if (comArray.Length > 0)
                {
                    comboBoxCOM.SelectedIndex = 0; // Select the first available port by default
                }
            }
        }

        private void btnMoveImg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                placeImageAt(Convert.ToInt32(txtMoveX.Text), Convert.ToInt32(txtMoveY.Text));
            }
            catch (Exception ex)
            {
                //string msg = "Commands successfully sent = " + countCmdSent + "\n" + ex.Message;
                MessageBox.Show(ex.Message, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

        }

        private void btnCenterImg_Click(object sender, RoutedEventArgs e)
        {
            if (btnCenterImg.Content.ToString().Contains("Center"))
            {
                double currentPicWidth = 0;
                double currentPicHeight = 0;
                if (Plottr.Filename.EndsWith(".svg"))
                {
                    currentPicWidth = svgPlot.GetImgWidth;
                    currentPicHeight = svgPlot.GetImgHeight;
                }
                else if (Plottr.Filename.EndsWith(".bmp"))
                {
                    currentPicWidth = myPlot.GetImgWidth;
                    currentPicHeight = myPlot.GetImgHeight;
                }
                Plottr.ImgMoveX = Convert.ToInt32((Plottr.RobotWidth - currentPicWidth ) / 2);
                Plottr.ImgMoveY = Convert.ToInt32((Plottr.RobotHeight - currentPicHeight) / 2);
                placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY);
                btnCenterImg.Content = "Move top left";
            }
            else if (btnCenterImg.Content.ToString().Contains("top left"))
            {
                Plottr.ImgMoveX = 0;
                Plottr.ImgMoveY = 0;
                placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY);
                btnCenterImg.Content = "Center image";
            }   
        }

        void placeImageAt(int x, int y)
        {
            if (Plottr.Filename.EndsWith(".bmp"))
                placeImageAt(x, y, myPlot, false);
            else if (Plottr.Filename.EndsWith(".svg"))
                placeImageAt(x, y, svgPlot, false);
        }

        void placeImageAt(int x, int y, Object o, bool isRetentionImg)
        {

            if(!isRetentionImg)
            {
                canvasPreview.Children.Clear();     //removes previous images/elements from the canvas
                canvasPreview.Background = System.Windows.Media.Brushes.White;

                Plottr.ImgMoveX = x;
                Plottr.ImgMoveY = y;

                if (Plottr.Filename.EndsWith(".svg"))
                {
                    SVGPlottr svg2 = o as SVGPlottr;
                    previewSVG(svg2);
                }
                else if (Plottr.Filename.EndsWith(".bmp"))
                {
                    PlottrBMP bmp2 = o as PlottrBMP;
                    previewBMP(bmp2, x, y);
                }
                //TODO create function that takes in object and previews on canvas to reuse on retenionImage

                txtMoveX.Text = Plottr.ImgMoveX.ToString();
                txtMoveY.Text = Plottr.ImgMoveY.ToString();

                if(retentionImage != null)
                    placeImageAt(x, y, retentionImage, true);
            }
            else
            {
                
                if (retentionImage.FileName.EndsWith(".bmp"))
                {
                    PlottrBMP bmp2 = new PlottrBMP(retentionImage.FileName);
                    previewBMP(bmp2, x, y);
                }
                else if (retentionImage.FileName.EndsWith(".svg"))
                {
                    SVGPlottr svg2 = new SVGPlottr(retentionImage.FileName);
                    previewSVG(svg2);
                }
            }

        }

        private void previewBMP(PlottrBMP o, int x, int y)
        {
            ImageBrush previewImageBrush = new ImageBrush(o.Img);
            previewImageBrush.Stretch = Stretch.Fill;
            previewImageBrush.ViewportUnits = BrushMappingMode.Absolute;
            previewImageBrush.Viewport = new Rect(x * scaleToPreview, y * scaleToPreview, o.GetImgWidth * scaleToPreview, o.GetImgHeight * scaleToPreview);     //fill the image to fit this box
            previewImageBrush.ViewboxUnits = BrushMappingMode.Absolute;
            previewImageBrush.Viewbox = new Rect(0, 0, o.Img.Width, o.Img.Height);    //set the image size to itself to avoid cropping
                                                                                            //txtOut.Text += previewImage.Width + "\n" + previewImage.Height + "\n";
            canvasPreview.Background = previewImageBrush;       //shows the image in the preview canvas
        }

        private void previewSVG(SVGPlottr o)
        {
            o.GenerateGCODE();
            o.GeneratePreviewPoints();
            //loadSVGPreviewPoints();
            foreach (PointF point in o.PreviewPoints)
            {
                Ellipse currentDot = new Ellipse();
                currentDot.Margin = new Thickness(point.X * scaleToPreview, point.Y * scaleToPreview, 0, 0);
                currentDot.Fill = System.Windows.Media.Brushes.DarkBlue;
                currentDot.Width = 2;
                currentDot.Height = 2;
                canvasPreview.Children.Add(currentDot);
            }
        }



        private void btnPauseDrawing_Click(object sender, RoutedEventArgs e)
        {
            // Only functional if connected and in a drawing state
            if (port == null || !port.IsOpen || (currentState != GUIStates.S6bmpDrawing && currentState != GUIStates.S9svgDrawing))
            {
                // Silently ignore or show a message if clicked inappropriately
                // MessageBox.Show("Not currently drawing.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (btnPauseDrawing.Content.ToString().Contains("Pause"))
            {
                // --- Request Pause ---
                // Set button text immediately for visual feedback.
                // The send loop (`btnSendImg_Click`) checks this text to stop sending.
                btnPauseDrawing.Content = "Continue drawing";
                // Optionally send Feed Hold ('!') if firmware supports it
                // try { port.Write("!"); } catch (Exception ex) { Console.WriteLine($"Error sending feed hold: {ex.Message}"); }
                txtOut.AppendText("Pause requested. Sending will stop after current command.\n");
                // DO NOT change state here. The send loop breaking will handle the state return.
            }
            else // Contains "Continue"
            {
                // --- Request Continue ---
                // Set button text back.
                btnPauseDrawing.Content = "Pause drawing";
                txtOut.AppendText("Resuming drawing...\n");
                // Optionally send Cycle Start ('~') if firmware supports it
                // try { port.Write("~"); } catch (Exception ex) { Console.WriteLine($"Error sending cycle start: {ex.Message}"); }

                // Re-call the send function. It will pick up from `countCmdSent`.
                // It will also handle transitioning back into the drawing state if needed (though it should already be in it).
                btnSendImg_Click(sender, e);
            }
        }


        private void btnCmdStart_Click(object sender, RoutedEventArgs e)
        {
            if (myPlot == null || myPlot.AllLines == null || !myPlot.AllLines.Any() || myPlot.GeneratedGCODE == null || !myPlot.GeneratedGCODE.Any())
            {
                // CORRECTED: Added MessageBoxButton.OK
                MessageBox.Show("No sliced BMP data available to start from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (currentState != GUIStates.S5bmpSlicedUsbConnected) // Can only start if sliced and connected
            {
                // CORRECTED: Added MessageBoxButton.OK
                MessageBox.Show("Please ensure USB is connected and BMP image is sliced.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                int lineNo = Convert.ToInt32(txtCmdStart.Text); // This is the desired LINE number

                // Validate line number against AllLines
                if (lineNo < 0 || lineNo >= myPlot.AllLines.Count)
                {
                    // CORRECTED: Added MessageBoxButton.OK
                    MessageBox.Show($"Line number {lineNo} is out of range (0 to {myPlot.AllLines.Count - 1}).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // --- Calculate the corresponding GCODE command index ---
                int calculatedIndex = 1 + (lineNo * 2); // Assumes 1 start command, 2 commands per line.

                // Validate calculated index against GCODE list bounds
                if (calculatedIndex < 0 || calculatedIndex >= myPlot.GeneratedGCODE.Count)
                {
                    // CORRECTED: Added MessageBoxButton.OK
                    MessageBox.Show($"Calculated starting GCODE command index ({calculatedIndex}) is out of range for the generated GCODE list ({myPlot.GeneratedGCODE.Count} commands). Check slicing logic.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Set the starting command index
                countCmdSent = calculatedIndex;

                txtOut.AppendText($"Set start to Line {lineNo} (GCODE command {countCmdSent + 1})\n");

                // Update slider to reflect the chosen start line
                sliderCmdCount.Value = lineNo;

                // Call the main send function to start drawing from this point
                btnSendImg_Click(sender, e);
            }
            catch (FormatException)
            {
                // CORRECTED: Added MessageBoxButton.OK
                MessageBox.Show("Invalid line number. Please enter a number.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                // CORRECTED: Added MessageBoxButton.OK
                MessageBox.Show($"Error setting start command: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnEnableStepper_Click(object sender, RoutedEventArgs e)
        {
            if (await sendSerialStringAsync("M17" + "\n"))
                txtOut.Text += "Timed out\n";
        }

        private async void btnDisableStepper_Click(object sender, RoutedEventArgs e)
        {
            if (await sendSerialStringAsync("M18" + "\n"))
                txtOut.Text += "Timed out\n";
        }

        private async void btnPenTouchCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (await sendSerialStringAsync("G1 Z0" + "\n"))
                txtOut.Text += "Timed out\n";
        }

        private async void btnNoPenTouchCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (await sendSerialStringAsync("G1 Z1" + "\n"))
                txtOut.Text += "Timed out\n";
        }

        private async void btnHomePosition_Click(object sender, RoutedEventArgs e)
        {
            if (await sendSerialStringAsync("G28" + "\n"))
                txtOut.Text += "Timed out\n";
        }

        private void sliderCmdCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Only update if BMP plot and lines exist (slider is only relevant for BMP)
            if (myPlot == null || myPlot.AllLines == null || !myPlot.AllLines.Any())
            {
                canvasPreview.Children.Remove(selectedPreviewLine); // Ensure highlight is removed if data disappears
                return;
            }

            int lineIndex = (int)e.NewValue;

            // Validate index before accessing AllLines
            if (lineIndex < 0 || lineIndex >= myPlot.AllLines.Count)
            {
                // Value might be temporarily out of range during Maximum update, ignore.
                // Or reset text if persistently invalid?
                // txtCmdStart.Text = "";
                canvasPreview.Children.Remove(selectedPreviewLine); // Remove highlight if index invalid
                return;
            }

            // Update the text box linked to the slider
            txtCmdStart.Text = lineIndex.ToString();

            // Highlight the selected line on the preview canvas
            canvasPreview.Children.Remove(selectedPreviewLine); // Remove previous highlight

            selectedPreviewLine.Stroke = System.Windows.Media.Brushes.Red;
            selectedPreviewLine.StrokeThickness = 2;
            selectedPreviewLine.X1 = myPlot.AllLines[lineIndex].X0 * scaleToPreview;
            selectedPreviewLine.Y1 = myPlot.AllLines[lineIndex].Y0 * scaleToPreview;
            selectedPreviewLine.X2 = myPlot.AllLines[lineIndex].X1 * scaleToPreview;
            selectedPreviewLine.Y2 = myPlot.AllLines[lineIndex].Y1 * scaleToPreview;

            canvasPreview.Children.Add(selectedPreviewLine); // Add new highlight
        }


        private void btnClearImg_Click(object sender, RoutedEventArgs e)
        {
            clearEverything(); // Perform the clear action first
            currentTransition = GUIActions.A2clear; // Set the transition cause
            handleGUIstates(); // Update the state machine AFTER action
        }

        private void clearEverything()
        {
            myPlot = null;
            svgPlot = null;
            retentionImage = null; // Also clear retention image
            Plottr.Filename = null; // Clear filename
            canvasPreview.Children.Clear();
            canvasPreview.Background = System.Windows.Media.Brushes.White;
            txtOut.AppendText("Cleared current image and GCODE.\n");

            // Reset relevant UI elements not covered by state machine enable/disable
            txtDpi.Text = "";
            txtMoveX.Text = "0";
            txtMoveY.Text = "0";
            if (sliderCmdCount.IsEnabled) // Only change if enabled (might be disabled in S0)
            {
                sliderCmdCount.Value = 0;
                sliderCmdCount.Maximum = 0;
            }
            txtCmdStart.Text = "0";
            btnHoldImg.Content = "Hold image"; // Reset hold button text

            // The state transition (A2clear) should be set *before* calling this method,
            // and handleGUIstates() called *after* this method finishes.
        }

        private void btnSliderIncDec(object sender, RoutedEventArgs e)      //increases or decreases the slider by one based on button press
        {
            if ((sender as Button).Content.ToString().Contains('<'))
                sliderCmdCount.Value--;
            else if ((sender as Button).Content.ToString().Contains('>'))
                sliderCmdCount.Value++;
        }

        private void txtCmdStart_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Return)
            {
                try
                {
                    sliderCmdCount.Value = Convert.ToInt32(txtCmdStart.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        private async void btnBoundingBox_Click(object sender, RoutedEventArgs e)
        {
            //selection box to choose if pen is touching canvas
            //define lines from stored coordinates
            //draw box in preview canvas
            //draw box on canvas
            //go to home position

            //BoundingCoordinates = new TraceLine(xMinVal, yMinVal, xMaxVal, yMaxVal);

            //txtOut.Text = myPlot.BoundingCoordinates.X0 + "\n";
            //txtOut.Text += myPlot.BoundingCoordinates.Y0 + "\n";
            //txtOut.Text += myPlot.BoundingCoordinates.X1 + "\n";
            //txtOut.Text += myPlot.BoundingCoordinates.Y1 + "\n";
            
            TraceLine topLeftToRight = new TraceLine(myPlot.BoundingCoordinates.X0 * scaleToPreview, myPlot.BoundingCoordinates.Y0 * scaleToPreview, myPlot.BoundingCoordinates.X1 * scaleToPreview, myPlot.BoundingCoordinates.Y0 * scaleToPreview);
            TraceLine rightDown = new TraceLine(myPlot.BoundingCoordinates.X1 * scaleToPreview, myPlot.BoundingCoordinates.Y0 * scaleToPreview, myPlot.BoundingCoordinates.X1 * scaleToPreview, myPlot.BoundingCoordinates.Y1 * scaleToPreview);
            TraceLine downRightToLeft = new TraceLine(myPlot.BoundingCoordinates.X1 * scaleToPreview, myPlot.BoundingCoordinates.Y1 * scaleToPreview, myPlot.BoundingCoordinates.X0 * scaleToPreview, myPlot.BoundingCoordinates.Y1 * scaleToPreview);
            TraceLine leftUp = new TraceLine(myPlot.BoundingCoordinates.X0 * scaleToPreview, myPlot.BoundingCoordinates.Y1 * scaleToPreview, myPlot.BoundingCoordinates.X0 * scaleToPreview, myPlot.BoundingCoordinates.Y0 * scaleToPreview);

            Line myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.Red;
            myLine.StrokeThickness = 2;
            myLine.X1 = topLeftToRight.X0;
            myLine.Y1 = topLeftToRight.Y0;
            myLine.X2 = topLeftToRight.X1;
            myLine.Y2 = topLeftToRight.Y1;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.Red;
            myLine.StrokeThickness = 2;
            myLine.X1 = rightDown.X0;
            myLine.Y1 = rightDown.Y0;
            myLine.X2 = rightDown.X1;
            myLine.Y2 = rightDown.Y1;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.Red;
            myLine.StrokeThickness = 2;
            myLine.X1 = downRightToLeft.X0;
            myLine.Y1 = downRightToLeft.Y0;
            myLine.X2 = downRightToLeft.X1;
            myLine.Y2 = downRightToLeft.Y1;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.Red;
            myLine.StrokeThickness = 2;
            myLine.X1 = leftUp.X0;
            myLine.Y1 = leftUp.Y0;
            myLine.X2 = leftUp.X1;
            myLine.Y2 = leftUp.Y1;
            canvasPreview.Children.Add(myLine);

            int penPosition = 1;    //pen not touching canvas
            if ((bool)checkBoxDrawingBoundingBox.IsChecked)
                penPosition = 0;

            try
            {
                await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X0, myPlot.BoundingCoordinates.Y0));    //goes from home position
                await sendSerialStringAsync(string.Format("G1 X{0} Y{1} Z{2}\n", myPlot.BoundingCoordinates.X1, myPlot.BoundingCoordinates.Y0, penPosition));    //draws first line
                await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X1, myPlot.BoundingCoordinates.Y1));              //draws second line
                await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X0, myPlot.BoundingCoordinates.Y1));  //draws third line
                await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X0, myPlot.BoundingCoordinates.Y0));                    //draws fourth line

                //if ((bool)checkBoxDrawingBoundingBox.IsChecked)
                await sendSerialStringAsync("G1 Z1\n");       //pen touches the canvas
                await sendSerialStringAsync("G28\n");                    //goes to home position
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

        }

        private void btnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            //zoomFactor increment
            //redraw content in canvas
            //fix scrollbars in canvas
            //eventuelt bare endre størrelse på canvas??

            calcCanvasPreviewScale(1.2);

            //TODO later: add zoom with mouse scroll wheel, and mouse click-to-drag
        }

        private void btnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            //canvasPreview.Width /= 1.1;
            //calcCanvasPreviewScale();
            //placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY);
            calcCanvasPreviewScale(1 / 1.2);
        }

        private void calcCanvasPreviewScale()
        {
            scaleToPreview = (double)canvasPreview.Width / (double)Plottr.RobotWidth;        //used to scale all actual sizes to be shown on screen
            canvasPreview.Height = Plottr.RobotHeight * scaleToPreview;

            scrollViewerHoldingCanvasPreview.ScrollToHorizontalOffset(scrollViewerHoldingCanvasPreview.ScrollableWidth / 2.0);
            scrollViewerHoldingCanvasPreview.ScrollToVerticalOffset(scrollViewerHoldingCanvasPreview.ScrollableHeight / 2.0);
            scrollViewerHoldingCanvasPreview.UpdateLayout();
        }

        private void calcCanvasPreviewScale(double scale)
        {

            canvasPreview.Width *= scale;
            calcCanvasPreviewScale();
            placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY);

            scrollViewerHoldingCanvasPreview.ScrollToHorizontalOffset(scrollViewerHoldingCanvasPreview.ScrollableWidth / 2.0);
            scrollViewerHoldingCanvasPreview.ScrollToVerticalOffset(scrollViewerHoldingCanvasPreview.ScrollableHeight / 2.0);
            scrollViewerHoldingCanvasPreview.UpdateLayout();

            //scrollViewerHoldingCanvasPreview.ScrollableHeight
        }

        
        void initCanvasPreview()        //draws dark grey frame around preview canvas
        {
            //canvasPreview.Width = robotWidth;
            //canvasPreview.Height = robotHeight;

            canvasPreview.Width = canvasPreview.Width;
            canvasPreview.Height = canvasPreview.Height;

            Line myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = 0;
            myLine.Y1 = 0;
            myLine.X2 = canvasPreview.Width;
            myLine.Y2 = 0;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = 0;
            myLine.Y1 = 0;
            myLine.X2 = 0;
            myLine.Y2 = canvasPreview.Height;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = 0;
            myLine.Y1 = canvasPreview.Height;
            myLine.X2 = canvasPreview.Width;
            myLine.Y2 = canvasPreview.Height;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = canvasPreview.Width;
            myLine.Y1 = 0;
            myLine.X2 = canvasPreview.Width;
            myLine.Y2 = canvasPreview.Height;
            canvasPreview.Children.Add(myLine);

            //System.Drawing.Rectangle myRect = new System.Drawing.Rectangle(0, 0, robotWidth, 200);
            
        }

        private void btnSaveDimension_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.RobotWidth = Convert.ToInt32(txtRWidth.Text);
            Properties.Settings.Default.RobotHeight = Convert.ToInt32(txtRHeight.Text);
            Properties.Settings.Default.Save();
            MessageBox.Show("Please restart the program for the changes to take effect.", "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }


        private Dictionary<(GUIStates, GUIActions), GUIStates> InitializeStateTransitions()
        {
            // NOTE: This dictionary now contains ALL transitions from the original logic
            return new Dictionary<(GUIStates, GUIActions), GUIStates>
            {
                // S0blank Transitions
                {(GUIStates.S0blank, GUIActions.A0bmpOpen), GUIStates.S1bmpLoaded},
                {(GUIStates.S0blank, GUIActions.A3svgOpen), GUIStates.S7svgLoaded},
                {(GUIStates.S0blank, GUIActions.A4usbOpen), GUIStates.S3usbConnected},

                // S1bmpLoaded Transitions
                {(GUIStates.S1bmpLoaded, GUIActions.A0bmpOpen), GUIStates.S1bmpLoaded}, // Reload BMP
                {(GUIStates.S1bmpLoaded, GUIActions.A1bmpSlice), GUIStates.S2bmpSliced},
                {(GUIStates.S1bmpLoaded, GUIActions.A2clear), GUIStates.S0blank},
                {(GUIStates.S1bmpLoaded, GUIActions.A3svgOpen), GUIStates.S7svgLoaded}, // Switch to SVG
                {(GUIStates.S1bmpLoaded, GUIActions.A4usbOpen), GUIStates.S4bmpLoadedUsbConnected},

                // S2bmpSliced Transitions
                {(GUIStates.S2bmpSliced, GUIActions.A0bmpOpen), GUIStates.S1bmpLoaded}, // Load new BMP, discard slice
                {(GUIStates.S2bmpSliced, GUIActions.A1bmpSlice), GUIStates.S2bmpSliced}, // Reslice
                {(GUIStates.S2bmpSliced, GUIActions.A2clear), GUIStates.S0blank},
                {(GUIStates.S2bmpSliced, GUIActions.A3svgOpen), GUIStates.S7svgLoaded}, // Switch to SVG
                {(GUIStates.S2bmpSliced, GUIActions.A4usbOpen), GUIStates.S5bmpSlicedUsbConnected},

                // S3usbConnected Transitions
                {(GUIStates.S3usbConnected, GUIActions.A0bmpOpen), GUIStates.S4bmpLoadedUsbConnected},
                {(GUIStates.S3usbConnected, GUIActions.A2clear), GUIStates.S3usbConnected}, // Clear image while USB connected
                {(GUIStates.S3usbConnected, GUIActions.A3svgOpen), GUIStates.S8svgLoadedUsbConnected},
                //{(GUIStates.S3usbConnected, GUIActions.A4usbOpen), GUIStates.S3usbConnected}, // Reconnecting USB? Original code had this, seems redundant if already connected.
                {(GUIStates.S3usbConnected, GUIActions.A6usbClose), GUIStates.S0blank},

                // S4bmpLoadedUsbConnected Transitions
                {(GUIStates.S4bmpLoadedUsbConnected, GUIActions.A0bmpOpen), GUIStates.S4bmpLoadedUsbConnected}, // Reload BMP
                {(GUIStates.S4bmpLoadedUsbConnected, GUIActions.A1bmpSlice), GUIStates.S5bmpSlicedUsbConnected},
                {(GUIStates.S4bmpLoadedUsbConnected, GUIActions.A2clear), GUIStates.S3usbConnected}, // Clear image
                {(GUIStates.S4bmpLoadedUsbConnected, GUIActions.A3svgOpen), GUIStates.S8svgLoadedUsbConnected}, // Switch to SVG
                //{(GUIStates.S4bmpLoadedUsbConnected, GUIActions.A4usbOpen), GUIStates.S4bmpLoadedUsbConnected}, // Reconnecting USB? Original code had this.
                {(GUIStates.S4bmpLoadedUsbConnected, GUIActions.A6usbClose), GUIStates.S1bmpLoaded}, // Disconnect USB

                // S5bmpSlicedUsbConnected Transitions
                {(GUIStates.S5bmpSlicedUsbConnected, GUIActions.A0bmpOpen), GUIStates.S4bmpLoadedUsbConnected}, // Load new BMP, discard slice
                {(GUIStates.S5bmpSlicedUsbConnected, GUIActions.A1bmpSlice), GUIStates.S5bmpSlicedUsbConnected}, // Reslice
                {(GUIStates.S5bmpSlicedUsbConnected, GUIActions.A2clear), GUIStates.S3usbConnected}, // Clear image
                {(GUIStates.S5bmpSlicedUsbConnected, GUIActions.A3svgOpen), GUIStates.S8svgLoadedUsbConnected}, // Switch to SVG
                //{(GUIStates.S5bmpSlicedUsbConnected, GUIActions.A4usbOpen), GUIStates.S5bmpSlicedUsbConnected}, // Reconnecting USB? Original code had this.
                {(GUIStates.S5bmpSlicedUsbConnected, GUIActions.A5startDrawing), GUIStates.S6bmpDrawing},
                {(GUIStates.S5bmpSlicedUsbConnected, GUIActions.A6usbClose), GUIStates.S2bmpSliced}, // Disconnect USB

                // S6bmpDrawing Transitions (Actions possible *while* drawing)
                {(GUIStates.S6bmpDrawing, GUIActions.A0bmpOpen), GUIStates.S4bmpLoadedUsbConnected}, // Stop drawing, load new BMP
                {(GUIStates.S6bmpDrawing, GUIActions.A2clear), GUIStates.S3usbConnected}, // Stop drawing, clear
                {(GUIStates.S6bmpDrawing, GUIActions.A3svgOpen), GUIStates.S8svgLoadedUsbConnected}, // Stop drawing, load SVG
                {(GUIStates.S6bmpDrawing, GUIActions.A5startDrawing), GUIStates.S5bmpSlicedUsbConnected}, // Drawing finished or paused -> return to sliced+connected state
                {(GUIStates.S6bmpDrawing, GUIActions.A6usbClose), GUIStates.S2bmpSliced}, // Stop drawing, disconnect USB

                // S7svgLoaded Transitions
                {(GUIStates.S7svgLoaded, GUIActions.A0bmpOpen), GUIStates.S1bmpLoaded}, // Switch to BMP
                {(GUIStates.S7svgLoaded, GUIActions.A2clear), GUIStates.S0blank},
                {(GUIStates.S7svgLoaded, GUIActions.A3svgOpen), GUIStates.S7svgLoaded}, // Reload SVG
                {(GUIStates.S7svgLoaded, GUIActions.A4usbOpen), GUIStates.S8svgLoadedUsbConnected},

                // S8svgLoadedUsbConnected Transitions
                {(GUIStates.S8svgLoadedUsbConnected, GUIActions.A0bmpOpen), GUIStates.S4bmpLoadedUsbConnected}, // Switch to BMP
                {(GUIStates.S8svgLoadedUsbConnected, GUIActions.A2clear), GUIStates.S3usbConnected}, // Clear image
                {(GUIStates.S8svgLoadedUsbConnected, GUIActions.A3svgOpen), GUIStates.S8svgLoadedUsbConnected}, // Reload SVG
                //{(GUIStates.S8svgLoadedUsbConnected, GUIActions.A4usbOpen), GUIStates.S8svgLoadedUsbConnected}, // Reconnecting USB? Original code had this.
                {(GUIStates.S8svgLoadedUsbConnected, GUIActions.A5startDrawing), GUIStates.S9svgDrawing},
                {(GUIStates.S8svgLoadedUsbConnected, GUIActions.A6usbClose), GUIStates.S7svgLoaded}, // Disconnect USB

                // S9svgDrawing Transitions (Actions possible *while* drawing)
                {(GUIStates.S9svgDrawing, GUIActions.A0bmpOpen), GUIStates.S4bmpLoadedUsbConnected}, // Stop drawing, load BMP
                {(GUIStates.S9svgDrawing, GUIActions.A2clear), GUIStates.S3usbConnected}, // Stop drawing, clear
                {(GUIStates.S9svgDrawing, GUIActions.A3svgOpen), GUIStates.S8svgLoadedUsbConnected}, // Stop drawing, reload SVG
                {(GUIStates.S9svgDrawing, GUIActions.A5startDrawing), GUIStates.S8svgLoadedUsbConnected}, // Drawing finished or paused -> return to loaded+connected state
                {(GUIStates.S9svgDrawing, GUIActions.A6usbClose), GUIStates.S7svgLoaded} // Stop drawing, disconnect USB
            };
        }

        // ================================================================
        // MODIFIED: Helper method to initialize the GUI state actions dictionary
        // (Replaces the old InitializeGuiStateActions with more complete logic)
        // ================================================================
        private void InitializeGuiStateActions()
        {
            // NOTE: This dictionary now enables controls matching the original logic more closely,
            //       plus additions like Zoom, Hold, DPI controls where appropriate.
            guiStateActions = new Dictionary<GUIStates, Action>
            {
                // S0blank: Nothing enabled except file/usb open
                {GUIStates.S0blank, () => { /* No extra controls enabled beyond default */ }},

                // S1bmpLoaded: BMP is loaded, allow move, center, clear, slice, zoom, hold, dpi
                {GUIStates.S1bmpLoaded, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnSliceImg, btnZoomIn, btnZoomOut, btnHoldImg, btnUpdateDpi, txtDpi)},

                // S2bmpSliced: BMP is sliced, allow previous + slice controls
                {GUIStates.S2bmpSliced, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnSliceImg, sliderCmdCount, btnSliderDecrease, btnSliderIncrease, btnZoomIn, btnZoomOut, btnHoldImg, btnUpdateDpi, txtDpi)},

                // S3usbConnected: USB connected, allow direct serial commands, plotter controls
                {GUIStates.S3usbConnected, () => EnableControls(txtSerialCmd, btnSend, btnEnableStepper, btnDisableStepper, btnPenTouchCanvas, btnNoPenTouchCanvas, btnHomePosition)},

                // S4bmpLoadedUsbConnected: BMP loaded and USB connected, allow S1 + S3 controls
                {GUIStates.S4bmpLoadedUsbConnected, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnSliceImg, txtSerialCmd, btnSend, btnEnableStepper, btnDisableStepper, btnPenTouchCanvas, btnNoPenTouchCanvas, btnHomePosition, btnZoomIn, btnZoomOut, btnHoldImg, btnUpdateDpi, txtDpi)},

                // S5bmpSlicedUsbConnected: BMP sliced and USB connected, allow S2 + S3 controls + drawing controls + bounding box
                {GUIStates.S5bmpSlicedUsbConnected, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnSliceImg, btnBoundingBox, checkBoxDrawingBoundingBox, btnPauseDrawing, btnSendImg, btnCmdStart, sliderCmdCount, btnSliderDecrease, btnSliderIncrease, txtSerialCmd, btnSend, btnEnableStepper, btnDisableStepper, btnPenTouchCanvas, btnNoPenTouchCanvas, btnHomePosition, btnZoomIn, btnZoomOut, btnHoldImg, btnUpdateDpi, txtDpi)},

                // S6bmpDrawing: BMP is drawing, allow pausing and direct plotter controls (matching S5 for now)
                {GUIStates.S6bmpDrawing, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnSliceImg, btnBoundingBox, checkBoxDrawingBoundingBox, btnPauseDrawing, btnSendImg, btnCmdStart, sliderCmdCount, btnSliderDecrease, btnSliderIncrease, txtSerialCmd, btnSend, btnEnableStepper, btnDisableStepper, btnPenTouchCanvas, btnNoPenTouchCanvas, btnHomePosition, btnZoomIn, btnZoomOut, btnHoldImg, btnUpdateDpi, txtDpi)},

                // S7svgLoaded: SVG is loaded, allow move, center, clear, zoom, hold
                {GUIStates.S7svgLoaded, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnZoomIn, btnZoomOut, btnHoldImg)},

                // S8svgLoadedUsbConnected: SVG loaded and USB connected, allow S7 + S3 controls + drawing (no slicing/bbox/slider for SVG)
                {GUIStates.S8svgLoadedUsbConnected, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnPauseDrawing, btnSendImg, /* btnCmdStart, sliderCmdCount, btnSliderDecrease, btnSliderIncrease, */ txtSerialCmd, btnSend, btnEnableStepper, btnDisableStepper, btnPenTouchCanvas, btnNoPenTouchCanvas, btnHomePosition, btnZoomIn, btnZoomOut, btnHoldImg)},

                // S9svgDrawing: SVG is drawing, allow pausing and direct plotter controls (matching S8 for now)
                {GUIStates.S9svgDrawing, () => EnableControls(txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnPauseDrawing, btnSendImg, /* btnCmdStart, sliderCmdCount, btnSliderDecrease, btnSliderIncrease, */ txtSerialCmd, btnSend, btnEnableStepper, btnDisableStepper, btnPenTouchCanvas, btnNoPenTouchCanvas, btnHomePosition, btnZoomIn, btnZoomOut, btnHoldImg)}
            };
        }

        // ================================================================
        // MODIFIED: State Machine Handling Logic
        // (Minor logging/error handling improvements)
        // ================================================================
        private void handleGUIstates()
        {
            if (stateTransitions.TryGetValue((currentState, currentTransition), out GUIStates newState))
            {
                Console.WriteLine($"State Transition: {currentState} --({currentTransition})--> {newState}"); // Log transition
                currentState = newState;
                updateGUIelements(); // Update GUI for the new state
            }
            else
            {
                // Log or show message for undefined transitions, but don't change state
                Console.WriteLine($"State Machine Warning: No defined transition from {currentState} with action {currentTransition}. State unchanged.");
                // MessageBox.Show($"Invalid action '{currentTransition}' for current state '{currentState}'.", "State Machine Info", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ================================================================
        // MODIFIED: GUI Update Logic
        // (Added specific control updates after general enablement)
        // ================================================================
        private void updateGUIelements()
        {
            // 1. Disable all controls managed by the state machine
            disableAllGUIelements();

            // 2. Enable controls based on the current state
            if (guiStateActions.TryGetValue(currentState, out Action enableAction))
            {
                enableAction(); // Execute the delegate to enable specific controls
            }
            else
            {
                Console.WriteLine($"State Machine Error: No GUI update action defined for state {currentState}.");
                MessageBox.Show($"Internal Error: Missing UI update logic for state {currentState}.", "State Machine Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 3. Update specific control states not handled by simple enable/disable
            // Update Connect button text and COM port dropdown based on port state
            if (port != null && port.IsOpen)
            {
                btnConnect.Content = "Disconnect";
                comboBoxCOM.IsEnabled = false; // Can't change port while connected
            }
            else
            {
                btnConnect.Content = "Connect USB";
                comboBoxCOM.IsEnabled = true; // Can change port when disconnected
            }

            // Update Hold button text based on retentionImage state
            if (retentionImage != null)
            {
                btnHoldImg.Content = "Release first image";
            }
            else
            {
                btnHoldImg.Content = "Hold image";
            }

            // Update Pause button text based on whether it's currently showing "Continue"
            // (This assumes the button text reflects the desired *next* action, not the current state)
            // A more robust way might use a separate boolean like `isPaused`.
            // If the state is NOT a drawing state, ensure it says "Pause"
            if (currentState != GUIStates.S6bmpDrawing && currentState != GUIStates.S9svgDrawing)
            {
                if (btnPauseDrawing.Content.ToString().Contains("Continue")) // Reset if not drawing
                {
                    btnPauseDrawing.Content = "Pause drawing";
                }
            }
            // If it IS a drawing state, the btnPauseDrawing_Click handler manages the text.
        }


        // ================================================================
        // MODIFIED: Helper Methods for GUI Updates
        // (Added more controls to disable list, added null checks)
        // ================================================================
        private void disableAllGUIelements()
        {
            var controlsToManage = new Control[] {
                txtMoveX, txtMoveY, btnMoveImg, btnCenterImg, btnClearImg, btnSliceImg,
                btnBoundingBox, checkBoxDrawingBoundingBox, btnPauseDrawing, btnSendImg, btnCmdStart,
                sliderCmdCount, btnSliderDecrease, btnSliderIncrease, txtSerialCmd, btnSend,
                btnEnableStepper, btnDisableStepper, btnPenTouchCanvas, btnNoPenTouchCanvas, btnHomePosition,
                btnZoomIn, btnZoomOut, btnHoldImg, btnUpdateDpi, txtDpi,
                // btnConnect and comboBoxCOM handled separately in updateGUIelements
            };

            foreach (var control in controlsToManage)
            {
                if (control != null) // Basic null check
                {
                    control.IsEnabled = false;
                }
            }
        }

        // MODIFIED: Added null check
        private static void EnableControls(params Control[] controls)
        {
            foreach (var control in controls)
            {
                if (control != null) // Basic null check
                {
                    control.IsEnabled = true;
                }
            }
        }


    }//main window
    }

