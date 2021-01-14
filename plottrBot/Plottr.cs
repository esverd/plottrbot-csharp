using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace plottrBot
{
    class Plottr
    {

        public BitmapImage Img { get; private set; }
        private bool[,] pixelArray { get; set; }        //array used for storing white/black pixels as true/false
        public List<TraceLine> BlackLines { get; private set; }     //stores all black lines to be drawn
        public List<TraceLine> AllLines { get; private set; }       //stores all movements as straight lines
        public List<string> GeneratedGCODE { get; set; }            //GCODE commands to be sent to the robot
        
        private int imgMoveX;
        public int ImgMoveX 
        { 
            get { return imgMoveX; } 
            set 
            {
                if (value < 0) imgMoveX = 0;
                else imgMoveX = value;
            } 
        }
        private int imgMoveY;
        public int ImgMoveY
        {
            get { return imgMoveY; }
            set
            {
                if (value < 0) imgMoveY = 0;
                else imgMoveY = value;
            }
        }
        private Bitmap TempImg { get; set; }
        static public double ToolDiameter { get; set; }
        public double GetImgWidth { get { return Img.Width * (25.4 / 96); } }      //gets width in mm
        public double GetImgHeight { get { return Img.Height * (25.4 / 96); } }    //gets height in mm
        public TraceLine BoundingCoordinates { get; set; }      //stores coordinates for bounding box around picture

        private double ratioWidthToPx { get; set; }
        private double ratioHeightToPx { get; set; }
        private int pxArrayWidth { get; set; }
        private int pxArrayHeight { get; set; }
        public string StartGCODE { get; set; }      //gcode that runs before the drawing starts
        public string EndGCODE { get; set; }        //gcode that runs when the drawing has finished

        static public bool TimedOut { get; set; }
        static public int RobotWidth { get; set; }
        static public int RobotHeight { get; set; }


        public Plottr(string filename)
        {
            Img = new BitmapImage(new Uri(filename, UriKind.Absolute));
            TempImg = new Bitmap(filename);
            BlackLines = new List<TraceLine>();
            AllLines = new List<TraceLine>();
            GeneratedGCODE = new List<string>();
            ImgMoveX = 0;
            ImgMoveY = 0;
            StartGCODE = "";
            EndGCODE = "";
        }

        private void imgToArray()
        {
            //ratioWidthToPx = usableImgWidth / Img.PixelWidth;     //calculates the ratio between picture width in mm and number of pixels in width
            //ratioHeightToPx = usableImgHeight / Img.PixelHeight;
            //pxArrayWidth = TempImg.Width;
            //pxArrayHeight = TempImg.Height;

            //this takes into consideration the maximum size of the robot canvas
            //areas outside the robot canvas gets cropped as they are unreachable
            ratioWidthToPx = GetImgWidth / Img.PixelWidth;     //calculates the ratio between picture width in mm and number of pixels in width
            ratioHeightToPx = GetImgHeight / Img.PixelHeight;

            double usableImgWidth = GetImgWidth;
            if (usableImgWidth - ImgMoveX > RobotWidth) usableImgWidth = RobotWidth - ImgMoveX;
            double usableImgHeight = GetImgHeight;
            if (usableImgHeight - ImgMoveY > RobotHeight) usableImgHeight = RobotHeight - ImgMoveY;

            pxArrayWidth = (int)(usableImgWidth / ratioWidthToPx);
            pxArrayHeight = (int)(usableImgHeight / ratioHeightToPx);
            


            //double dpi = 25.4 / ToolDiameter;
            //pxArrayWidth = (Int32)(ratioWidthToPx * dpi);
            //pxArrayHeight = (Int32)(ratioHeightToPx * dpi);

            //Bitmap tempImg = new Bitmap(1, 1);
            //using (MemoryStream outStream = new MemoryStream())
            //{
            //    BitmapEncoder enc = new BmpBitmapEncoder();
            //    enc.Frames.Add(BitmapFrame.Create(Img));
            //    enc.Save(outStream);
            //    System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

            //    Bitmap tempImp = new Bitmap(bitmap);
            //}

            int blackPixelThreshold = 60;

            pixelArray = new bool[pxArrayWidth, pxArrayHeight];
            for (int x = 0; x < pxArrayWidth; x++)
            {
                for (int y = 0; y < pxArrayHeight; y++)
                {
                    Color pixelColor = TempImg.GetPixel(x, y);
                    //Img.CopyPixels()
                    //if (pixelColor.R <= blackPixelThreshold && pixelColor.G <= blackPixelThreshold && pixelColor.B <= blackPixelThreshold)
                    if (pixelColor.R == pixelColor.G && pixelColor.R == pixelColor.B && pixelColor.R <= blackPixelThreshold)
                        pixelArray[x, y] = true;        //true = black pixel
                    else
                        pixelArray[x, y] = false;       //false = white pixel
                }
            }
        }



        private void calcMovementSideToSide()
        {
            imgToArray();
            BlackLines.Clear();

            bool lineStarted = false;
            int x0 = 0, y0 = 0;
            bool leftToRight = true;

            for (int y = 0; y < pxArrayHeight; y++)
            {
                for (int i = 0; i < pxArrayWidth - 1; i++)
                {
                    int x;
                    if (leftToRight) x = i;
                    else x = pxArrayWidth - 1 - i;

                    if (!lineStarted && pixelArray[x, y])       //if this is the first black pixel in a new line
                    {
                        x0 = x;         //saves coordinates for start of new line
                        y0 = y;
                        lineStarted = true;
                    }

                    int endX = x;
                    if(lineStarted)
                    {
                        if(leftToRight && (!pixelArray[x + 1, y] || x >= pxArrayWidth - 2))
                        {
                            if (pixelArray[x + 1, y])    //check the very last pixel as well
                                endX = x + 1;
                            lineStarted = false;        //start a new line
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (endX * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                        if(!leftToRight && (!pixelArray[x - 1, y] || x <= 1))
                        {
                            if (pixelArray[x - 1, y])    //check the very last pixel as well
                                endX = x + 1;
                            lineStarted = false;
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (endX * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                    }
                }
                leftToRight = !leftToRight;
            } //for y
        }

        private void calcMovementUpDown()
        {
            imgToArray();
            BlackLines.Clear();

            bool lineStarted = false;
            int x0 = 0, y0 = 0;
            bool goingDown = true;

            for (int x = 0; x < pxArrayWidth; x++)
            {
                for (int i = 0; i < pxArrayHeight - 1; i++)
                {
                    int y;
                    if (goingDown) y = i;
                    else y = pxArrayHeight - 1 - i;

                    if (!lineStarted && pixelArray[x, y])       //if this is the first black pixel in a new line
                    {
                        x0 = x;         //saves coordinates for start of new line
                        y0 = y;
                        lineStarted = true;
                    }

                    int endY = y;
                    if (lineStarted)
                    {
                        if (goingDown && (!pixelArray[x, y + 1] || y >= pxArrayHeight - 2))     //next downward pixel is white OR current array location is beyond pixel height
                        {
                            if (pixelArray[x, y + 1])    //check the very last pixel as well
                                endY = y + 1;
                            lineStarted = false;        //start a new line
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (endY * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                        if (!goingDown && (!pixelArray[x, y - 1] || y <= 1))
                        {
                            if (pixelArray[x, y - 1])    //check the very last pixel as well
                                endY = y - 1;       //endY = y + 1;
                            lineStarted = false;
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (endY * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                    }
                }
                goingDown = !goingDown;
            } //for x
        }


        public void GenerateGCODE()
        {
            //calcMovementSideToSide();
            calcMovementUpDown();
            AllLines.Clear();
            GeneratedGCODE.Clear();

            double xMinVal = BlackLines[0].X0;
            double xMaxVal = BlackLines[0].X1;
            double yMinVal = BlackLines[0].Y0;
            double yMaxVal = BlackLines[0].Y1;

            for (int i = 0; i < BlackLines.Count - 1; i++)
            {
                AllLines.Add(new TraceLine(BlackLines[i].X0, BlackLines[i].Y0, BlackLines[i].X1, BlackLines[i].Y1, true));      //stores the current line to be drawn
                AllLines.Add(new TraceLine(BlackLines[i].X1, BlackLines[i].Y1, BlackLines[i + 1].X0, BlackLines[i + 1].Y0, false));   //moves draw head to position for next line
                //bool drawWhileMoving = true;
                //if (BlackLines[i].Draw != BlackLines[i + 1].Draw)       //not valid as Draw is null for all BlackLines
                //    drawWhileMoving = false;
                //AllLines.Add(new TraceLine(BlackLines[i].X1, BlackLines[i].Y1, BlackLines[i + 1].X0, BlackLines[i + 1].Y0, drawWhileMoving)); //moves draw head to position for next line

                //TODO move to the very last BlackLines[i+1].X1 and Y1, this is currently not handled

                //finding the minimum and maximum values for both X and Y positions. used to draw the bounding box in GUI
                if (BlackLines[i].X0 < xMinVal || BlackLines[i].X1 < xMinVal)
                    xMinVal = Math.Min(BlackLines[i].X0, BlackLines[i].X1);
                if (BlackLines[i].X0 > xMaxVal || BlackLines[i].X1 > xMaxVal)
                    xMaxVal = Math.Max(BlackLines[i].X0, BlackLines[i].X1);
                if (BlackLines[i].Y0 < yMinVal || BlackLines[i].Y1 < yMinVal)
                    yMinVal = Math.Min(BlackLines[i].Y0, BlackLines[i].Y1);
                if (BlackLines[i].Y0 > yMaxVal || BlackLines[i].Y1 > yMaxVal)
                    yMaxVal = Math.Max(BlackLines[i].Y0, BlackLines[i].Y1);
            }

            BoundingCoordinates = new TraceLine(xMinVal, yMinVal, xMaxVal, yMaxVal);        //saves X and Y positions to draw bounding box later

            ////GeneratedGCODE.Add("G1 Z1\n");        //starts with the pen not touching the canvas
            GeneratedGCODE.Add(StartGCODE + "\n");
            GeneratedGCODE.Add(string.Format("G1 X{0} Y{1}\n", AllLines[0].X0, AllLines[0].Y0));    //goes from home position to start of first line to draw
            foreach (TraceLine line in AllLines)
            {
                GeneratedGCODE.Add("G1 Z" + Convert.ToInt32(!line.Draw) + "\n");
                GeneratedGCODE.Add(string.Format("G1 X{0} Y{1}\nL{2}", line.X1, line.Y1, AllLines.IndexOf(line)));      //added L to save the line number, makes gui stuff easier in main window
            }
            GeneratedGCODE.Add(EndGCODE + "\n");

            //GeneratedGCODE.Add("G1 Z1\n");        //starts with the pen not touching the canvas
            //GeneratedGCODE.Add(StartGCODE + "\n");
            //GeneratedGCODE.Add(string.Format("G1 X{0} Y{1}\n", AllLines[0].X0, AllLines[0].Y0));    //goes from home position to start of first line to draw
            //GeneratedGCODE.Add("G1 Z0\n");
            //for (int i = 0; i < AllLines.Count - 1; i++)
            //{
            //    if (AllLines[i].Draw != AllLines[i + 1].Draw)
            //        GeneratedGCODE.Add("G1 Z" + Convert.ToInt32(!AllLines[i].Draw) + "\n");
            //    GeneratedGCODE.Add(string.Format("G1 X{0} Y{1}\n", AllLines[i].X1, AllLines[i].Y1));
            //    //if this line draws black, and next line draws black -> nothing
            //    //if this line (draw) is not equal to next line (draw) -> change servo position
            //    //else keep the same servo position

            //}
            //GeneratedGCODE.Add(EndGCODE + "\n");

        }

        //public void RenderCircles()
        //{
        //    const int RADIUS = 40; //in pixels
        //}

        public void ImgToCSV()
        {
            using (StreamWriter outFile = new StreamWriter("imgArray.csv"))
            {
                for (int y = 0; y < Img.Height; y++)
                {
                    for (int x = 0; x < Img.Width; x++)
                        outFile.Write(pixelArray[x, y] + ",");
                    outFile.WriteLine();
                }
            }

        }

        //private void calMovementSideToSide()
        //{
        //    imgToArray();

        //    bool lineStarted = false;
        //    int x0 = 0, y0 = 0;
        //    bool leftToRight = true;

        //    for (int y = 0; y < pxArrayHeight; y++)
        //    {
        //        if (leftToRight)
        //        {
        //            for (int x = 0; x < pxArrayWidth - 1; x++)
        //            {
        //                if (!lineStarted && pixelArray[x, y])       //if this is the first black pixel in a new line
        //                {
        //                    x0 = x;         //saves coordinates for start of new line
        //                    y0 = y;
        //                    lineStarted = true;
        //                }
        //                if (lineStarted && (!pixelArray[x + 1, y] || x >= pxArrayWidth - 2))       //if next pixel is white or the edge of image
        //                {
        //                    if (pixelArray[x + 1, y])    //check the very last pixel as well
        //                        //BlackLines.Add(new TraceLine(x0 + ImgMoveX, y0 + ImgMoveY, (x + 1) + ImgMoveX, y + ImgMoveY));      //saves coordinates of last pixel in the line
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, ((x + 1) * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    else
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    lineStarted = false;        //start a new line
        //                }
        //            }
        //            leftToRight = false;
        //        }
        //        else
        //        {
        //            for (int x = pxArrayWidth - 1; x > 0; x--)
        //            {
        //                if (!lineStarted && pixelArray[x, y])
        //                {
        //                    x0 = x;
        //                    y0 = y;
        //                    lineStarted = true;
        //                }
        //                if (lineStarted && (!pixelArray[x - 1, y] || x <= 1))
        //                {
        //                    if (pixelArray[x - 1, y])    //check the very last pixel as well
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, ((x + 1) * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    else
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    lineStarted = false;
        //                }
        //            }
        //            leftToRight = true;
        //        }


        //    } //for y
        //}

    }


    class TraceLine
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public bool Draw { get; set; }

        public TraceLine(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public TraceLine(double x0, double y0, double x1, double y1, bool draw)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            Draw = draw;
        }

    }


    class SVGPlottr
    {
        public char Command { get; set; }
        public double[] PointValues { get; set; }
        public string Filepath { get; set; }
        public List<string> PathList { get; set; }

        public List<string> GeneratedGCODE { get; set; }

        //public List<string> GeneratedGCODE = new List<string>();

        public SVGPlottr()
        {
        }

        public SVGPlottr(string filepath)
        {
            Filepath = filepath;
            PathList = new List<string>();
            GeneratedGCODE = new List<string>();
            pathsToGCODE();
            //extract commands
            //generate gcode
        }

        private void pathsToGCODE()
        {
            using (StreamReader innFil = new StreamReader(Filepath))
            {
                while (!innFil.EndOfStream)     //reads a line in txt document until end of file is reached
                {
                    string currentLine = innFil.ReadLine();
                    if (currentLine.Contains("<path "))     //extracts line of related to describing a path
                        PathList.Add(currentLine);
                }
            }

            foreach (string path in PathList)
            {
                string[] splitGoose = path.Split(new[] { "d=\"m" }, StringSplitOptions.None);       //<path id="svg_4" d="m178.5,481.45...
                string cmd = 'm' + splitGoose[1].Substring(0, splitGoose[1].IndexOf('"'));      //extracts the bezier related info
                //txtOut.Text = cmd + "\n";

                //string testSplit = "a,b c,d e,f g";
                //string[] testSplit2 = testSplit.Split(new char[] { ',', ' ' });
                //foreach (string s in testSplit2)
                //    txtOut.Text += "\n" + s;

                List<int> commandIndex = new List<int>();
                for (int i = 0; i < cmd.Length; i++)        //extracts all command locations in the path
                {
                    if (Char.IsLetter(cmd[i]))
                    {
                        commandIndex.Add(i);
                        //txtOut.Text += cmd[i] + "\n";
                    }
                }

                //for
                //extract gcode (string = substring)
                    //add to gcode list

                for (int i = 0; i < commandIndex.Count; i++)
                {
                    if (i + 1 < commandIndex.Count)     //if not last item in loop
                    {
                        //extract gcode (string = substring)
                        //add to gcode list
                        string temp = cmd.Substring(commandIndex[i], commandIndex[i + 1] - commandIndex[i]);
                        GeneratedGCODE.Add(GenerateGCODE(cmd.Substring(commandIndex[i], commandIndex[i + 1] - commandIndex[i])));
                    }
                    else    //i + 1 = commandIndex.Count aka last object in list
                    {
                        //extract gcode (string = substring)
                        //add to gcode list
                        string temp = cmd.Substring(commandIndex[i], cmd.Length - commandIndex[i]);
                        GeneratedGCODE.Add(GenerateGCODE(cmd.Substring(commandIndex[i], cmd.Length - commandIndex[i])));
                    }

                }
                GeneratedGCODE.Add("G1 Z1\n");

                //string[] cmdSplit = cmd.Split(new char[] { ',', ' ' });

            }

        }

        public string GenerateGCODE(string pathCommand)
        {
            string returnString = "";

            char cmd = pathCommand[0];
            pathCommand = pathCommand.Substring(1, pathCommand.Length - 1);
            string[] cmdPointsString = pathCommand.Split(new char[] { ',', ' ' });
            List<double> cmdPoints = new List<double>();

            for (int i = 0; i < cmdPointsString.Length; i++)        //used to confirm each value is a number before GCODE is generated
                cmdPoints.Add(Convert.ToDouble(cmdPointsString[i]));

            switch (cmd)
            {
                case 'm':
                    returnString += "G1 Z1\n";
                    returnString += String.Format("G1 {0:0.##} {1:0.##}", cmdPoints[0], cmdPoints[1]);
                    break;
                case 'l':
                    returnString += "G1 Z0\n";
                    returnString += String.Format("G1 {0:0.##} {1:0.##}", cmdPoints[0], cmdPoints[1]);
                    break;
                case 'z':
                    break;
                case 'c':
                    returnString += String.Format("G5 c {0:0.##} {1:0.##} {2:0.##} {3:0.##} {4:0.##} {5:0.##}",
                        cmdPoints[0], cmdPoints[1], cmdPoints[2], cmdPoints[3], cmdPoints[4], cmdPoints[5]);
                    break;
                case 'q':
                    returnString += String.Format("G5 q {0:0.##} {1:0.##} {2:0.##} {3:0.##}",
                        cmdPoints[0], cmdPoints[1], cmdPoints[2], cmdPoints[3]);
                    break;
                default:
                    break;
            }

            return returnString;

        }



        //public SVGPlottr(char command, double x1, double y1)     //M L
        //{
        //    Command = command;
        //    PointValues[0] = x1;
        //    PointValues[1] = y1;
        //}

        //public SVGPlottr(char command, double x1, double y1, double x, double y)     //Q
        //{
        //    Command = command;
        //    PointValues[0] = x1;
        //    PointValues[1] = y1;
        //    PointValues[2] = x;
        //    PointValues[3] = y;
        //}

        //public SVGPlottr(char command, double x1, double y1, double x2, double y2, double x, double y)     //C
        //{
        //    Command = command;
        //    PointValues[0] = x1;
        //    PointValues[1] = y1;
        //    PointValues[2] = x2;
        //    PointValues[3] = y2;
        //    PointValues[4] = x;
        //    PointValues[5] = y;
        //}

        //public string CmdToString()
        //{
        //    string returnString = "";

        //    switch (Command)
        //    {
        //        case 'm':
        //            returnString += "G1 Z1\n";
        //            returnString += String.Format("G1 {0} {1}", PointValues[0], PointValues[1]);
        //            break;
        //        case 'l':
        //            returnString += "G1 Z0\n";
        //            returnString += String.Format("G1 {0} {1}", PointValues[0], PointValues[1]);
        //            break;
        //        case 'z':
        //            break;
        //        case 'c':
        //            returnString += String.Format("G5 c {0} {1} {2} {3} {4} {5}", 
        //                PointValues[0], PointValues[1], PointValues[2], PointValues[3], PointValues[4], PointValues[5]);
        //            break;
        //        case 'q':
        //            returnString += String.Format("G5 q {0} {1} {2} {3}",
        //                PointValues[0], PointValues[1], PointValues[2], PointValues[3]);
        //            break;
        //        default:
        //            break;
        //    }

        //    return returnString;
        //}

    }

}

