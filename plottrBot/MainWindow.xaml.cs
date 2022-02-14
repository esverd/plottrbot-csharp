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

            currentState = GUIStates.S0blank;
            updateGUIelements();
            //currentState = GUIStates.T0blank;
            //updateGUIelements();

            Plottr.StartGCODE = "G1 Z1\n";
            Plottr.EndGCODE = txtEndGcode.Text + "\n";


            retentionImage = null;
        }

        

        //canvasPreview.Children.Clear();     //removes previous images/elements from the canvas
        //canvasPreview.Background = System.Windows.Media.Brushes.White;

        private void btnSelectImg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Image file (*.bmp) | *.bmp|Vector file (*.svg) | *.svg";
                //bool result = (bool)openFileDialog.ShowDialog();
                if ((bool)openFileDialog.ShowDialog())
                {
                    clearEverything();     //removes previous images/elements from the canvas
                    
                    if (openFileDialog.FileName.EndsWith(".bmp"))      //loaded .bmp image
                    {
                        //loadedImgType = imgType.bmp;
                        currentTransition = GUIActions.A0bmpOpen;
                        handleGUIstates();

                        Plottr.Filename = openFileDialog.FileName;
                        myPlot = new PlottrBMP(Plottr.Filename);      //creates a plottr object with the selected image

                        if (retentionImage == null)
                        {
                            Plottr.ImgMoveX = Convert.ToInt32((Plottr.RobotWidth - myPlot.GetImgWidth) / 2);
                            Plottr.ImgMoveY = Convert.ToInt32((Plottr.RobotHeight - myPlot.GetImgHeight) / 2);
                        }
                        else
                        {
                            redrawRetentionImage();
                        }

                        placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, myPlot, false);

                    }
                    else if (openFileDialog.FileName.EndsWith(".svg"))      //loaded .svg image
                    {
                        //c# will split svg into following components: M, L, Z, C, Q
                        //c# will then send these components as gcode to the robot
                        //the robot will then read and handle the gcode calling on the necessary type ov movement function

                        //loadedImgType = imgType.svg;
                        currentTransition = GUIActions.A3svgOpen;
                        handleGUIstates();

                        Plottr.Filename = openFileDialog.FileName;
                        svgPlot = new SVGPlottr(Plottr.Filename);
                        //svgPlot.GeneratePreviewPoints();

                        //currentTransition = GUITransitions.H8svgOpen;
                        //handleGUIstates();
                        if (retentionImage == null)
                        {
                            Plottr.ImgMoveX = Convert.ToInt32((Plottr.RobotWidth - svgPlot.GetImgWidth) / 2);
                            Plottr.ImgMoveY = Convert.ToInt32((Plottr.RobotHeight - svgPlot.GetImgHeight) / 2);
                        }
                        else
                        {
                            redrawRetentionImage();
                        }


                        placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, svgPlot, false);
                    }
                    else
                        throw new Exception("Not supported file type");

                    //MessageBox.Show(Plottr.ImgMoveX.ToString(), "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    //placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, false);     //places the image in the center of preview canvas

                }
            }
            catch (Exception ex)
            {
                string msg = "Commands successfully sent = " + countCmdSent + "\n" + ex.Message;
                MessageBox.Show(msg, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void redrawRetentionImage()
        {
            //properties: fileName, retentionX, retentionY, ifSliced, fileType
            //Plottr.ImgMoveX = globalX;
            Plottr.ImgMoveX = retentionImage.ImgMoveX;
            Plottr.ImgMoveY = retentionImage.ImgMoveY;

            if(retentionImage.FileName.EndsWith(".bmp"))
            {

            }
            else if (retentionImage.FileName.EndsWith(".svg"))
            {

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

        private async void btnSliceImg_Click(object sender, RoutedEventArgs e)     //slices the image to individual lines that are either drawn or moved without drawing
        {
            countCmdSent = 0;

            //read start and end gcode from text boxes
            //Plottr.StartGCODE = "G1 Z1\n";
            //Plottr.EndGCODE = txtEndGcode.Text + "\n";
            //myPlot.GeneratedGCODE.Clear();
            //canvasPreview.Children.Clear();

            myPlot.Img.Freeze();        //bitmapimages needs to be frozen before they can be accessed by other threads
            previewBMPSlice(myPlot);

            sliderCmdCount.Maximum = myPlot.AllLines.Count - 1;

            txtOut.Text = "GCODE commands = " + myPlot.GeneratedGCODE.Count + "\nNumber of lines = " + myPlot.AllLines.Count + "\n";

            //previewing GCODE text is nice for debugging but super slow
            //foreach (string command in myPlot.GeneratedGCODE)
            //{
            //    txtOut.Text += command;     //prints the command to text output
            //}

            //if (btnSend.IsEnabled)
            //    enabledUIElements("both enable");
            //btnBoundingBox.IsEnabled = true;

            currentTransition = GUIActions.A1bmpSlice;
            handleGUIstates();
            //currentTransition = GUITransitions.H1imgSlice;
            //handleGUIstates();

            //SystemSounds.Exclamation.Play();
        }

        private async void previewBMPSlice(PlottrBMP o)
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

        private async void btnSendImg_Click(object sender, RoutedEventArgs e)       //send the whole sliced image to the robot over usb
        {
            try
            {
                if (Plottr.Filename.EndsWith(".bmp"))
                {
                    currentTransition = GUIActions.A5startDrawing;
                    handleGUIstates();
                    //currentTransition = GUITransitions.H5startDrawing;
                    //handleGUIstates();
                    bool timedOut = await sendSerialStringAsync("M220 S150\n");
                    txtOut.Text += String.Format("Drawing image. Starting at command {0} of {1}\n", countCmdSent, myPlot.GeneratedGCODE.Count);
                    //countCmdSent = 0 is set when an image is sliced
                    //countCmdSent = 400;
                    for (; countCmdSent < myPlot.GeneratedGCODE.Count; countCmdSent++)       //for loop instead of for each gives the possibility to start at a specific command
                    {
                        if (btnPauseDrawing.Content.ToString().Contains("Continue"))
                            break;

                        string[] getLineNo = myPlot.GeneratedGCODE[countCmdSent].Split('L');
                        if (int.TryParse(getLineNo[getLineNo.Count() - 1], out int lineNo))     //shows on slider the current line (not command) being drawn
                            sliderCmdCount.Value = lineNo;

                        timedOut = await sendSerialStringAsync(myPlot.GeneratedGCODE[countCmdSent]);     //sends the gcode over usb to the robot
                        if (timedOut)
                        {
                            txtOut.Text += "Timed out\n";
                            //break;      //exits the for loop
                        }

                        //countCmdSent = i;        //increment number of commands sent
                    }
                    txtOut.Text += "Commands successfully sent = " + countCmdSent + "\n";
                }
                //else if(loadedImgType == imgType.svg)
                //{
                //    currentTransition = GUIActions.A5startDrawing;
                //    handleGUIstates();

                //    bool timedOut = await sendSerialStringAsync("M220 S50\n");
                //    countCmdSent = 0;
                //    //if pause
                //    for (; countCmdSent < svgPlot.GeneratedGCODE.Count; countCmdSent++)       //for loop instead of for each gives the possibility to start at a specific command
                //    {
                //        txtOut.Text += "Timed out\n";
                //        //break;      //exits the for loop
                //    }
                //    txtOut.Text += "Commands successfully sent = " + countCmdSent + "\n";
                //}
                //else if(currentState == GUIStates.S8svgLoadedUsbConnected)
                else if (Plottr.Filename.EndsWith(".svg"))
                {
                    bool timedOut = await sendSerialStringAsync("M220 S50\n");
                    countCmdSent = 0;
                    //if pause
                    txtOut.Text += String.Format("Drawing image. Starting at command {0} of {1}\n", countCmdSent, svgPlot.GeneratedGCODE.Count);
                    for (; countCmdSent < svgPlot.GeneratedGCODE.Count; countCmdSent++)       //for loop instead of for each gives the possibility to start at a specific command
                    {
                        currentTransition = GUIActions.A5startDrawing;
                        handleGUIstates();
                        //currentTransition = GUITransitions.H5startDrawing;
                        //handleGUIstates();
                        if (btnPauseDrawing.Content.ToString().Contains("Continue"))
                            break;

                        timedOut = await sendSerialStringAsync(svgPlot.GeneratedGCODE[countCmdSent]);     //sends the gcode over usb to the robot
                        if (timedOut)
                            txtOut.Text += "Timed out\n";
                        //countCmdSent = i;        //increment number of commands sent
                    }
                    txtOut.Text += "Commands successfully sent = " + countCmdSent + "\n";
                }
                
            }
            catch (Exception ex)
            {
                string msg = "Commands successfully sent = " + countCmdSent + "\n" + ex.Message;
                MessageBox.Show(msg, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }
        private async void btnSend_Click(object sender, RoutedEventArgs e)        //send cmd
        {
            bool timedOut = await sendSerialStringAsync(txtSerialCmd.Text + "\n");
            if (timedOut)
                txtOut.Text += "Timed out\n";
        }

        private void txtSerialCmd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                btnSend_Click(sender, e);
        }

        private async Task<bool> sendSerialStringAsync(string message)
        {
            try
            {
                bool result = await Task.Run(() => 
                {
                    bool timedOut = false;
                    if (port.IsOpen)
                    {
                        port.Write(message);       //sends the current command over usb

                        //wait for GO from arduino
                        double WaitTimeout = (60 * 1000) + DateTime.Now.TimeOfDay.TotalMilliseconds;      //timeout is 20 seconds

                        string incoming = "";
                        while (!incoming.Contains("GO"))
                        {
                            if (port.BytesToRead > 0)
                                incoming = port.ReadLine();     //reads the reply from arduino
                            if ((DateTime.Now.TimeOfDay.TotalMilliseconds >= WaitTimeout))      //if the time elapsed is larger than the timeout
                            {
                                timedOut = true;        //flag timeout event
                                break;    
                            }
                        }
                    }
                    return timedOut;
                });

                if(result)
                    throw new Exception("Timed out. Recheck USB connection.");
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return true;
            }

        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(port != null && port.IsOpen)
                {
                    port.Close();
                    btnConnect.Content = "Connect USB";
                    currentTransition = GUIActions.A6usbClose;
                    handleGUIstates();
                    //currentTransition = GUITransitions.H4usbClose;
                    //handleGUIstates();
                }
                else
                {
                    port.PortName = comArray[comboBoxCOM.SelectedIndex];
                    port.BaudRate = 9600;
                    port.Parity = Parity.None;
                    port.DataBits = 8;
                    port.StopBits = StopBits.One;
                    port.Open();
                    btnConnect.Content = "Disconnect";
                    currentTransition = GUIActions.A4usbOpen;
                    handleGUIstates();
                    //currentTransition = GUITransitions.H3usbOpen;     
                    //handleGUIstates();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void comboBoxCOM_DropDownOpened(object sender, EventArgs e)
        {
            comArray = SerialPort.GetPortNames();
            comboBoxCOM.ItemsSource = comArray;
        }

        private void btnMoveImg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(Plottr.Filename.EndsWith(".svg"))
                    placeImageAt(Convert.ToInt32(txtMoveX.Text), Convert.ToInt32(txtMoveY.Text), svgPlot, false);
                else if (Plottr.Filename.EndsWith(".bmp"))
                    placeImageAt(Convert.ToInt32(txtMoveX.Text), Convert.ToInt32(txtMoveY.Text), myPlot, false);
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
                {
                    placeImageAt(x, y, retentionImage, true);
                }
            }
            else
            {
                
                if (retentionImage.FileName.EndsWith(".bmp"))
                {
                    //create new object
                    //recursively call this function, with argument true
                    //PlottrBMP o2 = new PlottrBMP(retentionImage.FileName);
                    //placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY, o2, true);

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
            if(btnPauseDrawing.Content.ToString().Contains("Pause"))
            {
                btnPauseDrawing.Content = "Continue drawing";
                txtOut.Text += "Commands successfully sent = " + countCmdSent + "\n";       //mismatch between this countCmdSent and the one used in btnCmdStart_Click
            }
            else
            {
                btnPauseDrawing.Content = "Pause drawing";
                btnSendImg_Click(sender, e);
            }
        }

        private void btnCmdStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int cmdNo = Convert.ToInt32(txtCmdStart.Text);
                countCmdSent = myPlot.GeneratedGCODE.IndexOf(string.Format("G1 X{0} Y{1}\n", myPlot.AllLines[cmdNo].X1, myPlot.AllLines[cmdNo].Y1));
                btnSendImg_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
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
            txtCmdStart.Text = ((int)sliderCmdCount.Value).ToString();

            canvasPreview.Children.Remove(selectedPreviewLine);
            selectedPreviewLine.Stroke = System.Windows.Media.Brushes.Red;
            selectedPreviewLine.StrokeThickness = 2;
            selectedPreviewLine.X1 = myPlot.AllLines[(int)sliderCmdCount.Value].X0 * scaleToPreview;
            selectedPreviewLine.Y1 = myPlot.AllLines[(int)sliderCmdCount.Value].Y0 * scaleToPreview;
            selectedPreviewLine.X2 = myPlot.AllLines[(int)sliderCmdCount.Value].X1 * scaleToPreview;
            selectedPreviewLine.Y2 = myPlot.AllLines[(int)sliderCmdCount.Value].Y1 * scaleToPreview;
            canvasPreview.Children.Add(selectedPreviewLine);
        }


        private void btnClearImg_Click(object sender, RoutedEventArgs e)
        {
            clearEverything();
            currentTransition = GUIActions.A2clear;
            handleGUIstates();
            //currentTransition = GUITransitions.H2imgClear;
            //handleGUIstates();
        } 

        private void clearEverything()
        {
            myPlot = null;
            svgPlot = null;
            canvasPreview.Children.Clear();     //removes previous images/elements from the canvas
            canvasPreview.Background = System.Windows.Media.Brushes.White;
            //currentTransition = GUITransitions.H2imgClear;
            //handleGUIstates();
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

            calcCanvasPreviewScale(1.1);

            //TODO later: add zoom with mouse scroll wheel, and mouse click-to-drag
        }

        private void btnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            //canvasPreview.Width /= 1.1;
            //calcCanvasPreviewScale();
            //placeImageAt(Plottr.ImgMoveX, Plottr.ImgMoveY);
            calcCanvasPreviewScale(1 / 1.1);
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



        private void disableAllGUIelements()
        {
            txtMoveX.IsEnabled = false;
            txtMoveY.IsEnabled = false;
            btnMoveImg.IsEnabled = false;
            btnCenterImg.IsEnabled = false;
            btnClearImg.IsEnabled = false;
            btnSliceImg.IsEnabled = false;

            btnBoundingBox.IsEnabled = false;
            btnPauseDrawing.IsEnabled = false;
            btnSendImg.IsEnabled = false;
            btnCmdStart.IsEnabled = false;

            sliderCmdCount.IsEnabled = false;
            btnSliderDecrease.IsEnabled = false;
            btnSliderIncrease.IsEnabled = false;

            txtSerialCmd.IsEnabled = false;
            btnSend.IsEnabled = false;
            btnEnableStepper.IsEnabled = false;
            btnDisableStepper.IsEnabled = false;
            btnPenTouchCanvas.IsEnabled = false;
            btnNoPenTouchCanvas.IsEnabled = false;
            btnHomePosition.IsEnabled = false;
        }

        private void updateGUIelements()
        {
            disableAllGUIelements();
            switch (currentState)
            {
                case GUIStates.S0blank:
                    break;
                case GUIStates.S1bmpLoaded:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    break;
                case GUIStates.S2bmpSliced:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    sliderCmdCount.IsEnabled = true;
                    btnSliderDecrease.IsEnabled = true;
                    btnSliderIncrease.IsEnabled = true;
                    break;
                case GUIStates.S3usbConnected:
                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;
                    break;
                case GUIStates.S4bmpLoadedUsbConnected:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;
                    break;
                case GUIStates.S5bmpSlicedUsbConnected:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    btnBoundingBox.IsEnabled = true;
                    btnPauseDrawing.IsEnabled = true;
                    btnSendImg.IsEnabled = true;
                    btnCmdStart.IsEnabled = true;
                    sliderCmdCount.IsEnabled = true;
                    btnSliderDecrease.IsEnabled = true;
                    btnSliderIncrease.IsEnabled = true;
                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;
                    break;
                case GUIStates.S6bmpDrawing:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    btnBoundingBox.IsEnabled = true;
                    btnPauseDrawing.IsEnabled = true;
                    btnSendImg.IsEnabled = true;
                    btnCmdStart.IsEnabled = true;
                    sliderCmdCount.IsEnabled = true;
                    btnSliderDecrease.IsEnabled = true;
                    btnSliderIncrease.IsEnabled = true;
                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;
                    break;
                case GUIStates.S7svgLoaded:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    break;
                case GUIStates.S8svgLoadedUsbConnected:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnPauseDrawing.IsEnabled = true;
                    btnSendImg.IsEnabled = true;
                    btnCmdStart.IsEnabled = true;
                    sliderCmdCount.IsEnabled = true;
                    btnSliderDecrease.IsEnabled = true;
                    btnSliderIncrease.IsEnabled = true;
                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;
                    break;
                case GUIStates.S9svgDrawing:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnPauseDrawing.IsEnabled = true;
                    btnSendImg.IsEnabled = true;
                    btnCmdStart.IsEnabled = true;
                    sliderCmdCount.IsEnabled = true;
                    btnSliderDecrease.IsEnabled = true;
                    btnSliderIncrease.IsEnabled = true;
                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;
                    break;
                default:
                    break;
            }
        }
        private void handleGUIstates()
        {
            switch (currentState)
            {
                case GUIStates.S0blank:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S1bmpLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S7svgLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S1bmpLoaded:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S1bmpLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A1bmpSlice:
                            currentState = GUIStates.S2bmpSliced;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S0blank;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S7svgLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S2bmpSliced:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S1bmpLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A1bmpSlice:
                            currentState = GUIStates.S2bmpSliced;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S0blank;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S7svgLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S5bmpSlicedUsbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S3usbConnected:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A6usbClose:
                            currentState = GUIStates.S0blank;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S4bmpLoadedUsbConnected:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A1bmpSlice:
                            currentState = GUIStates.S5bmpSlicedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A6usbClose:
                            currentState = GUIStates.S1bmpLoaded;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S5bmpSlicedUsbConnected:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A1bmpSlice:
                            currentState = GUIStates.S5bmpSlicedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A5startDrawing:
                            currentState = GUIStates.S6bmpDrawing;
                            updateGUIelements();
                            break;
                        case GUIActions.A6usbClose:
                            currentState = GUIStates.S2bmpSliced;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S6bmpDrawing:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A5startDrawing:
                            currentState = GUIStates.S5bmpSlicedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A6usbClose:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S7svgLoaded:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S1bmpLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S0blank;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S7svgLoaded;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S8svgLoadedUsbConnected:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A4usbOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A5startDrawing:
                            currentState = GUIStates.S9svgDrawing;
                            updateGUIelements();
                            break;
                        case GUIActions.A6usbClose:
                            currentState = GUIStates.S7svgLoaded;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.S9svgDrawing:
                    switch (currentTransition)
                    {
                        case GUIActions.A0bmpOpen:
                            currentState = GUIStates.S4bmpLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A2clear:
                            currentState = GUIStates.S3usbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A3svgOpen:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A5startDrawing:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUIActions.A6usbClose:
                            currentState = GUIStates.S8svgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
        }

        
    }//main window
    }

