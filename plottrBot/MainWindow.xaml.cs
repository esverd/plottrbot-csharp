﻿using Microsoft.Win32;
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


namespace plottrBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Plottr myPlot;          //the object ffrom the custom Plottr class
        string[] comArray;      //array for names of available COM ports
        SerialPort port;        //USB COM port object
        int robotWidth, robotHeight, previewWidth, previewHeight;
        double scaleToPreview;
        int countCmdSent;
        Line selectedPreviewLine;
        enum GUIStates { T0blank, T1imgLoaded, T2imgSliced, T3usbConnected, T4imgLoadedUsbConnected, T5imgSlicedUsbConnected, T6drawing };
        enum GUITransitions { H0imgOpen, H1imgSlice, H2imgClear, H3usbOpen, H4usbClose, H5startDrawing, H6pause };
        GUIStates currentState;
        GUITransitions currentTransition;

        public MainWindow()
        {
            InitializeComponent();
            
            robotWidth = 1460;      //in mm     //TODO load from settings/assets
            robotHeight = 1050; //1530 used for bigger canvas     //in mm     //TODO load from settings/assets
            previewWidth = 1200;
            scaleToPreview = (double)previewWidth / robotWidth;     //used to scale all actual sizes to be shown on screen
            previewHeight = (int)(robotHeight * scaleToPreview);
            borderPreview.Width = previewWidth;
            borderPreview.Height = previewHeight;
            tabControlOptions.Height = previewHeight + 25;
            
            //Plottr.ToolDiameter = 1.0;     //in mm     //TODO load from settings/assets
            //txtToolDiameter.Text = Plottr.ToolDiameter.ToString();

            port = new SerialPort();        //creates a blank serial port to be specified later

            selectedPreviewLine = new Line();

            currentState = GUIStates.T0blank;
            updateGUIelements();

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
                case GUIStates.T0blank:
                    break;
                case GUIStates.T1imgLoaded:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    break;
                case GUIStates.T2imgSliced:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    txtCmdStart.IsEnabled = true;
                    sliderCmdCount.IsEnabled = true;
                    btnSliderDecrease.IsEnabled = true;
                    btnSliderIncrease.IsEnabled = true;
                    break;
                case GUIStates.T3usbConnected:
                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;
                    break;
                case GUIStates.T4imgLoadedUsbConnected:
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
                case GUIStates.T5imgSlicedUsbConnected:
                case GUIStates.T6drawing:
                    txtMoveX.IsEnabled = true;
                    txtMoveY.IsEnabled = true;
                    btnMoveImg.IsEnabled = true;
                    btnCenterImg.IsEnabled = true;
                    btnClearImg.IsEnabled = true;
                    btnSliceImg.IsEnabled = true;
                    txtCmdStart.IsEnabled = true;
                    sliderCmdCount.IsEnabled = true;
                    btnSliderDecrease.IsEnabled = true;
                    btnSliderIncrease.IsEnabled = true;

                    btnBoundingBox.IsEnabled = true;
                    btnCmdStart.IsEnabled = true;

                    txtSerialCmd.IsEnabled = true;
                    btnSend.IsEnabled = true;
                    btnEnableStepper.IsEnabled = true;
                    btnDisableStepper.IsEnabled = true;
                    btnPenTouchCanvas.IsEnabled = true;
                    btnNoPenTouchCanvas.IsEnabled = true;
                    btnHomePosition.IsEnabled = true;

                    btnSendImg.IsEnabled = true;
                    btnPauseDrawing.IsEnabled = true;
                    break;
                //case GUIStates.T6drawing:
                //    break;
                default:
                    break;
            }
        }

        private void handleGUIstates()
        {
            switch (currentState)
            {
                case GUIStates.T0blank:
                    switch (currentTransition)
                    {
                        case GUITransitions.H0imgOpen:
                            currentState = GUIStates.T1imgLoaded;
                            updateGUIelements();
                            break;
                        case GUITransitions.H3usbOpen:
                            currentState = GUIStates.T3usbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T1imgLoaded:
                    switch (currentTransition)
                    {
                        case GUITransitions.H1imgSlice:
                            currentState = GUIStates.T2imgSliced;
                            updateGUIelements();
                            break;
                        case GUITransitions.H2imgClear:
                            currentState = GUIStates.T0blank;
                            updateGUIelements();
                            break;
                        case GUITransitions.H3usbOpen:
                            currentState = GUIStates.T4imgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T2imgSliced:
                    switch (currentTransition)
                    {
                        case GUITransitions.H2imgClear:
                            currentState = GUIStates.T0blank;
                            updateGUIelements();
                            break;
                        case GUITransitions.H3usbOpen:
                            currentState = GUIStates.T5imgSlicedUsbConnected;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T3usbConnected:
                    switch (currentTransition)
                    {
                        case GUITransitions.H0imgOpen:
                            currentState = GUIStates.T4imgLoadedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUITransitions.H4usbClose:
                            currentState = GUIStates.T0blank;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T4imgLoadedUsbConnected:
                    switch (currentTransition)
                    {
                        case GUITransitions.H1imgSlice:
                            currentState = GUIStates.T5imgSlicedUsbConnected;
                            updateGUIelements();
                            break;
                        case GUITransitions.H2imgClear:
                            currentState = GUIStates.T3usbConnected;
                            updateGUIelements();
                            break;
                        case GUITransitions.H4usbClose:
                            currentState = GUIStates.T1imgLoaded;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T5imgSlicedUsbConnected:
                    switch (currentTransition)
                    {
                        case GUITransitions.H2imgClear:
                            currentState = GUIStates.T3usbConnected;
                            updateGUIelements();
                            break;
                        case GUITransitions.H4usbClose:
                            currentState = GUIStates.T1imgLoaded;
                            updateGUIelements();
                            break;
                        case GUITransitions.H5startDrawing:
                            currentState = GUIStates.T6drawing;
                            updateGUIelements();
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T6drawing:
                    switch (currentTransition)
                    {
                        case GUITransitions.H2imgClear:
                            break;
                        case GUITransitions.H4usbClose:
                            break;
                        case GUITransitions.H5startDrawing:
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }

        }

        private void updateGUI2()
        {
            switch (currentState)
            {
                case GUIStates.T0blank:
                    switch (currentTransition)
                    {
                        case GUITransitions.H0imgOpen:
                            txtMoveX.IsEnabled = true;
                            txtMoveY.IsEnabled = true;
                            btnMoveImg.IsEnabled = true;
                            btnCenterImg.IsEnabled = true;
                            btnClearImg.IsEnabled = true;
                            btnSliceImg.IsEnabled = true;
                            currentState = GUIStates.T1imgLoaded;
                            break;
                        case GUITransitions.H3usbOpen:
                            txtSerialCmd.IsEnabled = true;
                            btnSend.IsEnabled = true;
                            btnEnableStepper.IsEnabled = true;
                            btnDisableStepper.IsEnabled = true;
                            btnPenTouchCanvas.IsEnabled = true;
                            btnNoPenTouchCanvas.IsEnabled = true;
                            btnHomePosition.IsEnabled = true;
                            currentState = GUIStates.T3usbConnected;
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T1imgLoaded:
                    switch (currentTransition)
                    {
                        case GUITransitions.H1imgSlice:
                            txtCmdStart.IsEnabled = true;
                            sliderCmdCount.IsEnabled = true;
                            btnSliderDecrease.IsEnabled = true;
                            btnSliderIncrease.IsEnabled = true;
                            currentState = GUIStates.T2imgSliced;
                            break;
                        case GUITransitions.H2imgClear:
                            disableAllGUIelements();
                            currentState = GUIStates.T0blank;
                            break;
                        case GUITransitions.H3usbOpen:
                            txtSerialCmd.IsEnabled = true;
                            btnSend.IsEnabled = true;
                            btnEnableStepper.IsEnabled = true;
                            btnDisableStepper.IsEnabled = true;
                            btnPenTouchCanvas.IsEnabled = true;
                            btnNoPenTouchCanvas.IsEnabled = true;
                            btnHomePosition.IsEnabled = true;
                            currentState = GUIStates.T4imgLoadedUsbConnected;
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T2imgSliced:
                    switch (currentTransition)
                    {
                        case GUITransitions.H2imgClear:
                            disableAllGUIelements();
                            currentState = GUIStates.T0blank;
                            break;
                        case GUITransitions.H3usbOpen:
                            txtSerialCmd.IsEnabled = true;
                            btnSend.IsEnabled = true;
                            btnEnableStepper.IsEnabled = true;
                            btnDisableStepper.IsEnabled = true;
                            btnPenTouchCanvas.IsEnabled = true;
                            btnNoPenTouchCanvas.IsEnabled = true;
                            btnHomePosition.IsEnabled = true;
                            btnBoundingBox.IsEnabled = true;
                            btnSendImg.IsEnabled = true;
                            btnCmdStart.IsEnabled = true;
                            currentState = GUIStates.T5imgSlicedUsbConnected;
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T3usbConnected:
                    switch (currentTransition)
                    {
                        case GUITransitions.H0imgOpen:
                            txtMoveX.IsEnabled = true;
                            txtMoveY.IsEnabled = true;
                            btnMoveImg.IsEnabled = true;
                            btnCenterImg.IsEnabled = true;
                            btnClearImg.IsEnabled = true;
                            btnSliceImg.IsEnabled = true;
                            currentState = GUIStates.T4imgLoadedUsbConnected;
                            break;
                        case GUITransitions.H4usbClose:
                            disableAllGUIelements();
                            currentState = GUIStates.T0blank;
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T4imgLoadedUsbConnected:
                    switch (currentTransition)
                    { 
                        case GUITransitions.H1imgSlice:
                            txtCmdStart.IsEnabled = true;
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
                            btnBoundingBox.IsEnabled = true;
                            btnSendImg.IsEnabled = true;
                            btnCmdStart.IsEnabled = true;
                            break;
                        case GUITransitions.H2imgClear:
                            txtMoveX.IsEnabled = false;
                            txtMoveY.IsEnabled = false;
                            btnMoveImg.IsEnabled = false;
                            btnCenterImg.IsEnabled = false;
                            btnClearImg.IsEnabled = false;
                            btnSliceImg.IsEnabled = false;
                            currentState = GUIStates.T3usbConnected;
                            break;
                        case GUITransitions.H4usbClose:
                            txtSerialCmd.IsEnabled = false;
                            btnSend.IsEnabled = false;
                            btnEnableStepper.IsEnabled = false;
                            btnDisableStepper.IsEnabled = false;
                            btnPenTouchCanvas.IsEnabled = false;
                            btnNoPenTouchCanvas.IsEnabled = false;
                            btnHomePosition.IsEnabled = false;
                            currentState = GUIStates.T1imgLoaded;
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T5imgSlicedUsbConnected:
                    switch (currentTransition)
                    {
                        case GUITransitions.H2imgClear:
                            txtMoveX.IsEnabled = false;
                            txtMoveY.IsEnabled = false;
                            btnMoveImg.IsEnabled = false;
                            btnCenterImg.IsEnabled = false;
                            btnClearImg.IsEnabled = false;
                            btnSliceImg.IsEnabled = false;
                            btnBoundingBox.IsEnabled = false;
                            btnSendImg.IsEnabled = false;
                            btnCmdStart.IsEnabled = false;
                            currentState = GUIStates.T3usbConnected;
                            break;
                        case GUITransitions.H4usbClose:
                            txtSerialCmd.IsEnabled = false;
                            btnSend.IsEnabled = false;
                            btnEnableStepper.IsEnabled = false;
                            btnDisableStepper.IsEnabled = false;
                            btnPenTouchCanvas.IsEnabled = false;
                            btnNoPenTouchCanvas.IsEnabled = false;
                            btnHomePosition.IsEnabled = false;
                            currentState = GUIStates.T1imgLoaded;
                            break;
                        case GUITransitions.H5startDrawing:
                            btnPauseDrawing.IsEnabled = true;
                            break;
                        default:
                            break;
                    }
                    break;
                case GUIStates.T6drawing:
                    switch (currentTransition)
                    {
                        case GUITransitions.H2imgClear:
                            break;
                        case GUITransitions.H4usbClose:
                            break;
                        case GUITransitions.H5startDrawing:
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }

        }

        private void btnSelectImg_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image file (*.bmp) | *.bmp";
            //bool result = (bool)openFileDialog.ShowDialog();
            if ((bool)openFileDialog.ShowDialog())
            {
                myPlot = new Plottr(openFileDialog.FileName);      //creates a plottr object with the selected image

                canvasPreview.Children.Clear();     //removes previous images/elements from the canvas
                myPlot.ImgMoveX = Convert.ToInt32((robotWidth - myPlot.GetImgWidth) / 2);
                myPlot.ImgMoveY = Convert.ToInt32((robotHeight - myPlot.GetImgHeight) / 2);
                placeImageAt(myPlot.ImgMoveX, myPlot.ImgMoveY);     //places the image in the center of preview canvas

                //enables buttons that need the image to work
                //enabledUIElements("img enable");
                currentTransition = GUITransitions.H0imgOpen;
                handleGUIstates();
            }
        }

        private async void btnSliceImg_Click(object sender, RoutedEventArgs e)     //slices the image to individual lines that are either drawn or moved without drawing
        {
            countCmdSent = 0;   //0;

            //read start and end gcode from text boxes
            myPlot.StartGCODE = "G1 Z1\n";
            myPlot.EndGCODE = txtEndGcode.Text + "\n";
            //myPlot.GeneratedGCODE.Clear();
            //canvasPreview.Children.Clear();

            myPlot.Img.Freeze();        //bitmapimages needs to be frozen before they can be accessed by other threads
            await Task.Run(() => myPlot.GenerateGCODE());       //generates the GCODE to send to the robot

            Dispatcher.Invoke(() =>
            {
                canvasPreview.Children.Clear();
                canvasPreview.Background = System.Windows.Media.Brushes.White;
                //draws preview lines that the robot is going to move along:
                foreach (TraceLine lineCommand in myPlot.AllLines)
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

            sliderCmdCount.Maximum = myPlot.AllLines.Count - 1;

            txtOut.Text = "GCODE commands = " + myPlot.GeneratedGCODE.Count + "\nNumber of lines = " + myPlot.AllLines.Count + "\n";      //takes a lot of time

            //previewing GCODE text is nice for debugging but super slow
            //foreach (string command in myPlot.GeneratedGCODE)
            //{
            //    txtOut.Text += command;     //prints the command to text output
            //}

            //if (btnSend.IsEnabled)
            //    enabledUIElements("both enable");
            //btnBoundingBox.IsEnabled = true;

            currentTransition = GUITransitions.H1imgSlice;
            handleGUIstates();

            //SystemSounds.Exclamation.Play();
        }

        private async void btnSendImg_Click(object sender, RoutedEventArgs e)       //send the whole sliced image to the robot over usb
        {
            txtOut.Text += String.Format("Drawing image. Starting at command {0} of {1}\n", countCmdSent, myPlot.GeneratedGCODE.Count);
            btnPauseDrawing.IsEnabled = true;
            
            try
            {
                //countCmdSent = 0 is set when an image is sliced
                for (; countCmdSent < myPlot.GeneratedGCODE.Count; countCmdSent++)       //for loop instead of for each gives the possibility to start at a specific command
                {
                    if (btnPauseDrawing.Content.ToString().Contains("Continue"))
                        break;
                    bool timedOut = await sendSerialStringAsync(myPlot.GeneratedGCODE[countCmdSent]);     //sends the gcode over usb to the robot
                    if (timedOut)
                    {
                        txtOut.Text += "Timed out\n";
                        //break;      //exits the for loop
                    }
                    //countCmdSent = i;        //increment number of commands sent
                }
                txtOut.Text += "Commands successfully sent = " + countCmdSent + "\n";

                currentTransition = GUITransitions.H5startDrawing;
                handleGUIstates();
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

        private bool sendSerialString(string message)       //not used anymore
        {
            try
            {
                bool timedOut = false;
                if (port.IsOpen)
                {
                    port.Write(message);       //sends the current command over usb

                    //wait for GO from arduino
                    double WaitTimeout = (20 * 1000) + DateTime.Now.TimeOfDay.TotalMilliseconds;      //timeout is 20 seconds

                    string incoming = "";
                    while (!incoming.Contains("GO"))
                    {
                        if(port.BytesToRead > 0)
                            incoming = port.ReadLine();     //reads the reply from arduino
                        if ((DateTime.Now.TimeOfDay.TotalMilliseconds >= WaitTimeout))      //if the time elapsed is larger than the timeout
                        {
                            timedOut = true;        //flag timeout event
                            //txtOut.Text += "Timed out";
                            throw new Exception("Timed out. Recheck USB connection.");
                        }
                    }
                }
                else
                    throw new Exception("Connect USB COM port");
                return timedOut;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Info", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return true;
            }

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

        private void enabledUIElements(string elements)
        {
            if(elements.Contains("com enable"))
            {
                btnSend.IsEnabled = true;
                txtSerialCmd.IsEnabled = true;
            }
            else if (elements.Contains("com disable"))
            {
                //btnSend.IsEnabled = false;
                //txtSerialCmd.IsEnabled = false;
                btnSendImg.IsEnabled = false;
                btnBoundingBox.IsEnabled = false;
            }
            else if (elements.Contains("img enable"))
            {
                btnSliceImg.IsEnabled = true;
                btnMoveImg.IsEnabled = true;
                btnCenterImg.IsEnabled = true;
                btnCenterImg.Content = "Move top left";
                txtMoveX.IsEnabled = true;
                txtMoveY.IsEnabled = true;
            }
            else if(elements.Contains("both enable"))
            {
                
                btnSendImg.IsEnabled = true;
                btnBoundingBox.IsEnabled = true;
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
                    //enabledUIElements("com disable");
                    currentTransition = GUITransitions.H4usbClose;
                    handleGUIstates();
                }
                else
                {
                    //port = new SerialPort(comArray[comboBoxCOM.SelectedIndex], 9600, Parity.None, 8, StopBits.One);
                    port.PortName = comArray[comboBoxCOM.SelectedIndex];
                    port.BaudRate = 9600;
                    port.Parity = Parity.None;
                    port.DataBits = 8;
                    port.StopBits = StopBits.One;
                    port.Open();
                    btnConnect.Content = "Disconnect";
                    //if(btnSliceImg.IsEnabled)
                    //    enabledUIElements("both enable");
                    //else
                    //    enabledUIElements("com enable");
                    currentTransition = GUITransitions.H3usbOpen;
                    handleGUIstates();
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
            myPlot.ImgMoveX = Convert.ToInt32(txtMoveX.Text);
            myPlot.ImgMoveY = Convert.ToInt32(txtMoveY.Text);
            placeImageAt(myPlot.ImgMoveX, myPlot.ImgMoveY);
        }

        private void btnCenterImg_Click(object sender, RoutedEventArgs e)
        {
            if (btnCenterImg.Content.ToString().Contains("Center"))
            {
                myPlot.ImgMoveX = Convert.ToInt32((robotWidth - myPlot.GetImgWidth) / 2);
                myPlot.ImgMoveY = Convert.ToInt32((robotHeight - myPlot.GetImgHeight) / 2);
                placeImageAt(myPlot.ImgMoveX, myPlot.ImgMoveY);
                btnCenterImg.Content = "Move top left";
            }
            else if (btnCenterImg.Content.ToString().Contains("top left"))
            {
                myPlot.ImgMoveX = 0;
                myPlot.ImgMoveY = 0;
                placeImageAt(myPlot.ImgMoveX, myPlot.ImgMoveY);
                btnCenterImg.Content = "Center image";
            }   
        }

        void placeImageAt(double x, double y)
        {
            canvasPreview.Children.Clear();     //removes potential preview lines already drawn
            ImageBrush previewImageBrush = new ImageBrush(myPlot.Img);
            previewImageBrush.Stretch = Stretch.Fill;
            previewImageBrush.ViewportUnits = BrushMappingMode.Absolute;
            previewImageBrush.Viewport = new Rect(x * scaleToPreview, y * scaleToPreview, myPlot.GetImgWidth * scaleToPreview, myPlot.GetImgHeight * scaleToPreview);     //fill the image to fit this box
            previewImageBrush.ViewboxUnits = BrushMappingMode.Absolute;
            previewImageBrush.Viewbox = new Rect(0, 0, myPlot.Img.Width, myPlot.Img.Height);    //set the image size to itself to avoid cropping

            //txtOut.Text += previewImage.Width + "\n" + previewImage.Height + "\n";
            canvasPreview.Background = previewImageBrush;       //shows the image in the preview canvas
            txtMoveX.Text = myPlot.ImgMoveX.ToString();
            txtMoveY.Text = myPlot.ImgMoveY.ToString();
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
                //countCmdSent = GeneratedGCODE.Add(string.Format("G1 X{0} Y{1}\n", line.X1, line.Y1));
                
                //string temp = (string.Format("G1 X{0} Y{1}\n", myPlot.AllLines[cmdNo].X1, myPlot.AllLines[cmdNo].Y1));

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
            myPlot = null;
            canvasPreview.Children.Clear();     //removes previous images/elements from the canvas
            canvasPreview.Background = System.Windows.Media.Brushes.White;
            currentTransition = GUITransitions.H2imgClear;
            handleGUIstates();
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


            await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X0, myPlot.BoundingCoordinates.Y0));    //goes from home position

            //if ((bool)checkBoxDrawingBoundingBox.IsChecked)
            //    await sendSerialStringAsync(string.Format("G1 X{0} Y{1} Z0\n", myPlot.BoundingCoordinates.X1, myPlot.BoundingCoordinates.Y0));    //draws first line
            //else
            //    await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X1, myPlot.BoundingCoordinates.Y0));    //draws first line
            await sendSerialStringAsync(string.Format("G1 X{0} Y{1} Z{2}\n", myPlot.BoundingCoordinates.X1, myPlot.BoundingCoordinates.Y0, !(bool)checkBoxDrawingBoundingBox.IsChecked));    //draws first line

            await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X1, myPlot.BoundingCoordinates.Y1));              //draws second line
            await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X0, myPlot.BoundingCoordinates.Y1));  //draws third line
            await sendSerialStringAsync(string.Format("G1 X{0} Y{1}\n", myPlot.BoundingCoordinates.X0, myPlot.BoundingCoordinates.Y0));                    //draws fourth line

            if ((bool)checkBoxDrawingBoundingBox.IsChecked)
                await sendSerialStringAsync("G1 Z1\n");       //pen touches the canvas
            await sendSerialStringAsync("G28\n");                    //goes to home position

        }

        void initCanvasPreview()        //draws dark grey frame around preview canvas
        {
            //canvasPreview.Width = robotWidth;
            //canvasPreview.Height = robotHeight;

            canvasPreview.Width = previewWidth;
            canvasPreview.Height = previewHeight;

            Line myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = 0;
            myLine.Y1 = 0;
            myLine.X2 = previewWidth;
            myLine.Y2 = 0;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = 0;
            myLine.Y1 = 0;
            myLine.X2 = 0;
            myLine.Y2 = previewHeight;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = 0;
            myLine.Y1 = previewHeight;
            myLine.X2 = previewWidth;
            myLine.Y2 = previewHeight;
            canvasPreview.Children.Add(myLine);

            myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.DarkGray;
            myLine.StrokeThickness = 3;
            myLine.X1 = previewWidth;
            myLine.Y1 = 0;
            myLine.X2 = previewWidth;
            myLine.Y2 = previewHeight;
            canvasPreview.Children.Add(myLine);

            //System.Drawing.Rectangle myRect = new System.Drawing.Rectangle(0, 0, robotWidth, 200);
            
        }

    }//main window
}
