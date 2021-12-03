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
        static public int RobotWidth { get; set; }
        static public int RobotHeight { get; set; }
        public List<string> GeneratedGCODE { get; set; }            //GCODE commands to be sent to the robot
        static public string StartGCODE { get; set; }      //gcode that runs before the drawing starts
        static public string EndGCODE { get; set; }        //gcode that runs when the drawing has finished

        static private int imgMoveX;
        static public int ImgMoveX
        {
            get { return imgMoveX; }
            set
            {
                if (value < 0) imgMoveX = 0;
                else imgMoveX = value;
            }
        }
        static private int imgMoveY;
        static public int ImgMoveY
        {
            get { return imgMoveY; }
            set
            {
                if (value < 0) imgMoveY = 0;
                else imgMoveY = value;
            }
        }

        //public void GenerateGCODE()
        //{

        //}

    }

    class PlottrBMP : Plottr
    {
        public BitmapImage Img { get; private set; }
        private bool[,] pixelArray { get; set; }        //array used for storing white/black pixels as true/false
        public List<TraceLine> BlackLines { get; private set; }     //stores all black lines to be drawn
        public List<TraceLine> AllLines { get; private set; }       //stores all movements as straight lines
        
        private Bitmap TempImg { get; set; }
        static public double ToolDiameter { get; set; }
        public double GetImgWidth { get { return Img.Width * (25.4 / 96); } }      //gets width in mm
        public double GetImgHeight { get { return Img.Height * (25.4 / 96); } }    //gets height in mm
        public TraceLine BoundingCoordinates { get; set; }      //stores coordinates for bounding box around picture

        private double ratioWidthToPx { get; set; }
        private double ratioHeightToPx { get; set; }
        private int pxArrayWidth { get; set; }
        private int pxArrayHeight { get; set; }
        
        static public bool TimedOut { get; set; }
        
        public PlottrBMP(string filename)
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

            int blackPixelThreshold = 70;

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

            GeneratedGCODE.Add(StartGCODE + "\n");
            GeneratedGCODE.Add(string.Format("G1 X{0:0.###} Y{1:0.###}\n", AllLines[0].X0, AllLines[0].Y0));    //goes from home position to start of first line to draw
            foreach (TraceLine line in AllLines)
            {
                GeneratedGCODE.Add("G1 Z" + Convert.ToInt32(!line.Draw) + "\n");
                GeneratedGCODE.Add(string.Format("G1 X{0:0.###} Y{1:0.###}\nL{2}", line.X1, line.Y1, AllLines.IndexOf(line)));      //added L to save the line number, makes gui stuff easier in main window
            }
            GeneratedGCODE.Add(EndGCODE + "\n");
        }

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

<<<<<<< Updated upstream
=======
<<<<<<< Updated upstream
=======
>>>>>>> Stashed changes

    class SVGPlottr : Plottr
    {
        public char Command { get; set; }
        public double[] PointValues { get; set; }
        public string Filepath { get; set; }
        public List<string> PathList { get; set; }
        private double relativeToAbsX { get; set; }
        private double relativeToAbsY { get; set; }
        private double startXforClose { get; set; }
        private double startYforClose { get; set; }
        public List<PointF> PreviewPoints { get; set; }
        private double svgWidth { get; set; }
        private double svgHeight { get; set; }
        public double GetImgWidth { get { return svgWidth; } }      //gets width in mm
        public double GetImgHeight { get { return svgHeight; } }    //gets height in mm
        private double bmpDimensionOffsetWidth { get; set; }        //used to correct for when bmp images converted to arrays have dimensions set in ints
        private double bmpDimensionOffsetHeight { get; set; }

        //public SVGPlottr()      //for debugging remove when svg works
        //{
        //}
        public SVGPlottr(string filepath)
        {
            Filepath = filepath;
            relativeToAbsX = 0;
            relativeToAbsY = 0;
            
            PathList = new List<string>();
            GeneratedGCODE = new List<string>();
            PreviewPoints = new List<PointF>();
            getAllPaths();
            GenerateGCODE();
        }

        //public string outPathString { get; set; }
        private void getAllPaths()
        {
            using (StreamReader innFil = new StreamReader(Filepath))
            {
                while (!innFil.EndOfStream)     //reads a line in txt document until end of file is reached
                {
                    string currentLine = innFil.ReadLine();
                    string pathString = "";
                    if (currentLine.Contains(" width="))
                        svgWidth = Convert.ToDouble(currentLine.Substring(currentLine.IndexOf('"') + 1, currentLine.IndexOf("mm") - currentLine.IndexOf('"') - 1));
                    else if (currentLine.Contains(" height="))
                        svgHeight = Convert.ToDouble(currentLine.Substring(currentLine.IndexOf('"') + 1, currentLine.IndexOf("mm") - currentLine.IndexOf('"') - 1));
                    else if (currentLine.Contains("<path"))     //extracts path from svg file from "<path" to "/>" including all information in between
                    {
                        while(!currentLine.Contains("/>"))
                        {
                            pathString += currentLine;
                            currentLine = innFil.ReadLine();
                        }
                        pathString += currentLine;
                        PathList.Add(pathString);
                        //outPathString = pathString;
                    }
                }
            }

        }

        public void GeneratePreviewPoints()
        {
            PreviewPoints.Clear();

            double currentX = RobotWidth / 2.0;
            double currentY = RobotHeight / 2.0;
            foreach (string gcode in GeneratedGCODE)
            {
                if (gcode.Contains("G1"))
                {
                    if(gcode.Contains('X'))
                        currentX = exctractCoordFromString(gcode, 'X');
                    if (gcode.Contains('Y'))
                        currentY = exctractCoordFromString(gcode, 'Y');
                    if (gcode.Contains("Z0"))
                        PreviewPoints.Add(new PointF((float)currentX, (float)currentY));
                }
                else if (gcode.Contains("G5 C"))
                {
                    double iVal = exctractCoordFromString(gcode, 'I');
                    double jVal = exctractCoordFromString(gcode, 'J');
                    double kVal = exctractCoordFromString(gcode, 'K');
                    double lVal = exctractCoordFromString(gcode, 'L');
                    double xVal = exctractCoordFromString(gcode, 'X');
                    double yVal = exctractCoordFromString(gcode, 'Y');
                    for (double t = 0.0; t <= 1.0; t += 0.03)
                    {
                        double nextX = Math.Pow((1 - t), 3) * currentX + 3 * Math.Pow((1 - t), 2) * t * iVal + 3 * (1 - t) * Math.Pow(t, 2) * kVal + Math.Pow(t, 3) * xVal;
                        double nextY = Math.Pow((1 - t), 3) * currentY + 3 * Math.Pow((1 - t), 2) * t * jVal + 3 * (1 - t) * Math.Pow(t, 2) * lVal + Math.Pow(t, 3) * yVal;
                        PreviewPoints.Add(new PointF((float)nextX, (float)nextY));
                    }
                    currentX = xVal;
                    currentY = yVal;
                }
                else if (gcode.Contains("G5 Q"))
                {
                    double iVal = exctractCoordFromString(gcode, 'I');
                    double jVal = exctractCoordFromString(gcode, 'J');
                    double xVal = exctractCoordFromString(gcode, 'X');
                    double yVal = exctractCoordFromString(gcode, 'Y');
                    for (double t = 0.0; t <= 1.0; t += 0.03)
                    {
                        double nextX = currentX * Math.Pow((1 - t), 2) + 2 * t * iVal * (1 - t) + xVal * Math.Pow(t, 2);
                        double nextY = currentY * Math.Pow((1 - t), 2) + 2 * t * jVal * (1 - t) + yVal * Math.Pow(t, 2);
                        PreviewPoints.Add(new PointF((float)nextX, (float)nextY));
                    }
                    currentX = xVal;
                    currentY = yVal;
                }
            }
        }

        private double exctractCoordFromString(string coordinateString, char coordinateAxis)
        {
            string foundCoord = "-1";
            char findChar = coordinateAxis;
            if (coordinateString.IndexOf(findChar) != -1)   //extracts the X value from the incomming command
            {
                //foundCoord = coordinateString.Substring(coordinateString.IndexOf(findChar) + 1, (coordinateString.Substring(coordinateString.IndexOf(findChar) + 1, coordinateString.IndexOf(findChar) + 2)).IndexOf(' ') - coordinateString.IndexOf(findChar) + 1);
                foundCoord = coordinateString.Substring(coordinateString.IndexOf(findChar) + 1);
                if(foundCoord.IndexOf(' ') != -1)
                    foundCoord = foundCoord.Substring(0, foundCoord.IndexOf(' '));
            }
            return Convert.ToDouble(foundCoord);
            //return 1;
        }

        public void GenerateGCODE()
        {
            GeneratedGCODE.Clear();
            GeneratedGCODE.Add(StartGCODE);

            //+ImgMoveY
            bmpDimensionOffsetWidth = GetImgWidth - (int)(GetImgWidth);
            bmpDimensionOffsetHeight = GetImgHeight - (int)(GetImgHeight);
            //ImgMoveY = GetImgWidth;

            foreach (string path in PathList)
            {
                startXforClose = -1;
                startYforClose = -1;
                string[] splitGoose;
                string cmd = "";
                if (path.Contains("d=\"M"))     //all svg commands begins with moving to the start point for the path
                {
                    splitGoose = path.Split(new[] { "d=\"M" }, StringSplitOptions.None);       //<path id="svg_4" d="M 178.5,481.45...
                    cmd = 'M' + splitGoose[1].Substring(0, splitGoose[1].IndexOf('"'));      //extracts the bezier related info
                }    
                else if (path.Contains("d=\"m"))
                {
                    splitGoose = path.Split(new[] { "d=\"m" }, StringSplitOptions.None);       //<path id="svg_4" d="m178.5,481.45...
                    cmd = 'm' + splitGoose[1].Substring(0, splitGoose[1].IndexOf('"'));      //extracts the bezier related info
                }

                List<int> commandIndex = new List<int>();
                for (int i = 0; i < cmd.Length; i++)        //extracts all command locations given in the path as characters
                {
                    if (Char.IsLetter(cmd[i]))
                        commandIndex.Add(i);
                }

                //this is where the index problem seems to be located
                for (int i = 0; i < commandIndex.Count; i++)    //splits the path commands into separate strings for each command
                {
                    string temp = "";
                    if (i + 1 < commandIndex.Count)     //if not last item in loop
                        GeneratedGCODE.Add(pathCommandToGCODE(cmd.Substring(commandIndex[i], commandIndex[i + 1] - commandIndex[i])));  //adds GCODE based on the path command
                        //temp = pathCommandToGCODE(cmd.Substring(commandIndex[i], commandIndex[i + 1] - commandIndex[i]));
                    else    //i + 1 = commandIndex.Count aka last object in list
                        GeneratedGCODE.Add(pathCommandToGCODE(cmd.Substring(commandIndex[i], cmd.Length - commandIndex[i])));   //adds GCODE based on the path command
                    //temp = cmd.Substring(commandIndex[i], cmd.Length - commandIndex[i]);
                }
            }
            GeneratedGCODE.Add(EndGCODE);
        }

        //WORKS
        public string pathCommandToGCODE(string pathCommand)   //converts bezier commands from svg path to GCODE which the robot can interpret
        {
            char cmd = pathCommand[0];      //only the command
            pathCommand = pathCommand.Substring(1, pathCommand.Length - 1);     //all of the command parameteres/coordinates
            List<string> cmdPointsString = (pathCommand.Split(new char[] { ',', ' ' })).ToList<string>();   //creates new list split on spaces and commas
            List<double> cmdPoints = new List<double>();        //used to store converted numbers which are going to be sent to the robot

            foreach (string number in cmdPointsString)      //makes sure all read coordinates are numbers and removes stray spaces and other characters
            {
                //double parsedNumber = 0;
                //double.TryParse(number, out parsedNumber);
                //if(parsedNumber != 0)
                //    cmdPoints.Add(parsedNumber);
                double parsedNumber;
                if (double.TryParse(number, out parsedNumber))
                    cmdPoints.Add(parsedNumber);
            }

            //for (int i = 0; i < cmdPoints.Count; i++)       //handles relative to absolute coordinates and changed position on canvas
            //{
            //    //TODO: I really need to check and debug the logic for ImgMove and relativeToAbs, as this is completely untested. it makes sense at this point tho
            //    if (i % 2 == 0)     //if coordinate on x axis
            //    {
            //        cmdPoints[i] = cmdPoints[i] + ImgMoveX;// + relativeToAbsX;
            //        relativeToAbsX += cmdPoints[i] - ImgMoveX;
            //    }
            //    else                //if coordinate on y axis
            //    {
            //        cmdPoints[i] = cmdPoints[i] + ImgMoveY;// + relativeToAbsY;
            //        relativeToAbsY += cmdPoints[i] - ImgMoveY;
            //    }
            //}

            return putStringTogether(cmd, cmdPoints);
        }

        //WORKS
        private string putStringTogether(char cmd, List<double> cmdPoints)      //handles if the list has multiples of required points for cmd
        {
            string returnString = "";
            int numberOfPointsForCmd = 0;

            double totalOffsetWidth = ImgMoveX - bmpDimensionOffsetWidth;
<<<<<<< Updated upstream
            double totalOffsetHeight = ImgMoveY - bmpDimensionOffsetHeight;
=======
            double totalOffsetHeight = ImgMoveY - bmpDimensionOffsetHeight + 2;     //temporary correction for misalignment between bmp and svg
>>>>>>> Stashed changes

            switch (cmd)
            {
                case 'm':
                case 'M':
                    returnString += String.Format("G1 X{0:0.###} Y{1:0.###} Z1\n", cmdPoints[0] + totalOffsetWidth, cmdPoints[1] + totalOffsetHeight);
                    numberOfPointsForCmd = 2;
                    if(startXforClose == -1 && startYforClose == -1)
                    {
                        startXforClose = cmdPoints[0];
                        startYforClose = cmdPoints[1];
                    }
                    break;
                //case 'l':
                case 'L':
                    returnString += String.Format("G1 X{0:0.###} Y{1:0.###} Z0\n", cmdPoints[0] + totalOffsetWidth, cmdPoints[1] + totalOffsetHeight);
                    numberOfPointsForCmd = 2;
                    break;
                case 'z':
                case 'Z':
                    returnString += String.Format("G1 X{0:0.###} Y{1:0.###} Z0\n", startXforClose + totalOffsetWidth, startYforClose + totalOffsetHeight);
                    break;
                //case 'c':
                case 'C':
                    //returnString += "G1 Z0\n";
                    returnString += String.Format("G5 C I{0:0.###} J{1:0.###} K{2:0.###} L{3:0.###} X{4:0.###} Y{5:0.###} Z0\n",
                        cmdPoints[0] + totalOffsetWidth, cmdPoints[1] + totalOffsetHeight, cmdPoints[2] + totalOffsetWidth, cmdPoints[3] + totalOffsetHeight, cmdPoints[4] + totalOffsetWidth, cmdPoints[5] + totalOffsetHeight);
                    numberOfPointsForCmd = 6;
                    break;
                //case 'q':
                case 'Q':
                    //returnString += "G1 Z0\n";
                    returnString += String.Format("G5 Q I{0:0.###} J{1:0.###} X{2:0.###} Y{3:0.###} Z0\n",
                        cmdPoints[0] + totalOffsetWidth, cmdPoints[1] + totalOffsetHeight, cmdPoints[2] + totalOffsetWidth, cmdPoints[3] + totalOffsetHeight);
                    numberOfPointsForCmd = 4;
                    break;
                case 'V':
                    returnString += String.Format("G1 Y{0:0.###} Z0\n", cmdPoints[0] + totalOffsetHeight);
                    numberOfPointsForCmd = 1;
                    break;
                case 'H':
                    returnString += String.Format("G1 X{0:0.###} Z0\n", cmdPoints[0] + totalOffsetWidth);
                    numberOfPointsForCmd = 1;
                    break;
                default:
                    break;
            }

            if (cmdPoints.Count > numberOfPointsForCmd)      //some sweet recursion to handle one character denoting a repeat of commands
                returnString += putStringTogether(cmd, cmdPoints.GetRange(numberOfPointsForCmd, cmdPoints.Count - numberOfPointsForCmd));

            return returnString;
        }

    }

<<<<<<< Updated upstream
=======
>>>>>>> Stashed changes
>>>>>>> Stashed changes
}

