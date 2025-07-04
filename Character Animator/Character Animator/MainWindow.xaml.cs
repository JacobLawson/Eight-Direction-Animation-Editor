using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace Character_Animator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public class AnimationData
    {
        public Dictionary<int, List<Border>> angleToFrames = new();
        public bool reduced_set_flag = false;
    }

    public struct AnimationJsonData
    {
        public List<List<JsonElement>> animation { get; set; }
        public bool reduced { get; set; }
    }

    public partial class MainWindow : Window
    {
        private BitmapImage spriteSheet;

        private Dictionary<string, AnimationData> animationLibrary = new();
        private string currentAnimation;

        private Dictionary<int, List<Border>> angleBuffer = new();
        private Border[,] frameGridData;

        private bool suppressSave = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadSpriteSheet_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg)|*.png;*.jpg"
            };

            if (ofd.ShowDialog() == true)
            {
                spriteSheet = new BitmapImage(new Uri(ofd.FileName));
                int frameWidth = int.Parse(FrameWidthBox.Text);
                int frameHeight = int.Parse(FrameHeightBox.Text);
                DisplayFrames(spriteSheet, frameWidth, frameHeight);
            }
        }

        private void DisplayFrames(BitmapImage sheet, int frameWidth, int frameHeight)
        {
            FrameGrid.Children.Clear();
            angleBuffer.Clear();

            for (int angle = 0; angle < 360; angle += 45)
                angleBuffer[angle] = new List<Border>();

            int paddedWidth = frameWidth + 1;
            int paddedHeight = frameHeight + 1;

            int columns = sheet.PixelWidth / paddedWidth;
            int rows = sheet.PixelHeight / paddedHeight;

            int gridWidth = columns * frameWidth;
            FrameGrid.Width = gridWidth;

            frameGridData = new Border[rows, columns];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int sourceX = x * paddedWidth + 1;
                    int sourceY = y * paddedHeight + 1;

                    if (sourceX + frameWidth > sheet.PixelWidth || sourceY + frameHeight > sheet.PixelHeight)
                        continue;

                    int actualWidth = Math.Min(frameWidth, sheet.PixelWidth - sourceX);
                    int actualHeight = Math.Min(frameHeight, sheet.PixelHeight - sourceY);

                    if (actualWidth <= 0 || actualHeight <= 0)
                        continue;

                    Int32Rect rect = new Int32Rect(sourceX, sourceY, actualWidth, actualHeight);
                    CroppedBitmap frame = new CroppedBitmap(sheet, rect);

                    Image img = new Image
                    {
                        Source = frame,
                        Width = frameWidth,
                        Height = frameHeight,
                        Stretch = Stretch.None
                    };

                    var frameDetailsGrid = new Grid();

                    TextBlock angleLabel = new TextBlock
                    {
                        Text = "",
                        Foreground = Brushes.Yellow,
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        Margin = new Thickness(2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.Black,
                            Direction = 0,
                            ShadowDepth = 0,
                            Opacity = 0.7,
                            BlurRadius = 4
                        }
                    };

                    TextBlock frameNumberLabel = new TextBlock
                    {
                        Text = "",
                        Foreground = Brushes.Yellow,
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        Margin = new Thickness(2),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.Black,
                            Direction = 0,
                            ShadowDepth = 0,
                            Opacity = 0.7,
                            BlurRadius = 4
                        }
                    };

                    frameDetailsGrid.Children.Add(angleLabel);
                    frameDetailsGrid.Children.Add(frameNumberLabel);
                    frameDetailsGrid.Children.Add(img);

                    Border border = new Border
                    {
                        BorderThickness = new Thickness(2),
                        BorderBrush = Brushes.Transparent,
                        Margin = new Thickness(2),
                        Child = frameDetailsGrid,
                        Tag = false // not selected initially
                    };

                    frameGridData[y, x] = border;

                    int frameX = x;
                    int frameY = y;

                    border.MouseLeftButtonUp += (s, e) =>
                    {
                        int baseRow = frameY;
                        int maxRows = frameGridData.GetLength(0);

                        if (MirrorSelectBox.IsChecked == true)
                        {
                            var reduced_set = ReducedSpriteSet.IsChecked == true ? 5 : 8;

                            // Toggle selection for all up to 8 frames in this column starting at baseRow
                            for (int r = baseRow; r < Math.Min(baseRow + reduced_set, maxRows); r++)
                            {
                                Border b = frameGridData[r, frameX];
                                if (b != null)
                                    ToggleFrameSelection(b, r - baseRow);
                            }
                        }
                        else
                        {
                            ToggleFrameSelection(border, 0);
                        }

                        UpdateLabels();
                    };

                    UpdateLabels();
                    FrameGrid.Children.Add(border);
                }
            }
        }

        private void ToggleFrameSelection(Border b, int relativeAngleIndex)
        {
            bool isSelected = b.Tag is bool selected && selected;

            int angle = relativeAngleIndex * 45;

            if (isSelected)
            {
                // Deselect
                b.Tag = false;
                b.BorderBrush = Brushes.Transparent;

                if (angleBuffer.ContainsKey(angle))
                    angleBuffer[angle].Remove(b);
            }
            else
            {
                // Select
                b.Tag = true;
                b.BorderBrush = Brushes.Red;

                if (!angleBuffer.ContainsKey(angle))
                    angleBuffer[angle] = new List<Border>();

                if (!angleBuffer[angle].Contains(b))
                    angleBuffer[angle].Add(b);
            }
        }

        private void UpdateLabels()
        {
            // First, clear all labels
            int rows = frameGridData.GetLength(0);
            int columns = frameGridData.GetLength(1);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Border b = frameGridData[y, x];
                    if (b != null && b.Child is Grid g)
                    {
                        if (g.Children[0] is TextBlock angleLabel)
                            angleLabel.Text = "";
                        if (g.Children[1] is TextBlock numberLabel)
                            numberLabel.Text = "";
                    }
                }
            }

            // Now assign labels per angle
            foreach (var kvp in angleBuffer)
            {
                int angle = kvp.Key;
                List<Border> frames = kvp.Value;

                for (int i = 0; i < frames.Count; i++)
                {
                    Border b = frames[i];
                    if (b.Child is Grid g)
                    {
                        if (g.Children[0] is TextBlock angleLabel)
                            angleLabel.Text = $"{angle}°";
                        if (g.Children[1] is TextBlock numberLabel)
                            numberLabel.Text = i.ToString(); // <-- index within angle
                    }
                }
            }
        }

        private void ClearAllSelections()
        {
            for (int angle = 0; angle < 360; angle += 45)
            {
                if (angleBuffer.ContainsKey(angle))
                    angleBuffer[angle].Clear();
            }

            int rows = frameGridData.GetLength(0);
            int columns = frameGridData.GetLength(1);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Border b = frameGridData[y, x];
                    if (b != null)
                    {
                        b.Tag = false;
                        b.BorderBrush = Brushes.Transparent;

                        if (b.Child is Grid g)
                        {
                            if (g.Children[0] is TextBlock angleLabel)
                                angleLabel.Text = "";
                            if (g.Children[1] is TextBlock numberLabel)
                                numberLabel.Text = "";
                        }
                    }
                }
            }
        }

        private void AddAnimation_Click(object sender, RoutedEventArgs e)
        {
            string name = NewAnimationNameBox.Text.Trim();
            if (!string.IsNullOrEmpty(name) && !animationLibrary.ContainsKey(name))
            {
                animationLibrary[name] = new AnimationData();
                AnimationList.Items.Add(name);
                NewAnimationNameBox.Clear();
            }
        }

        private void AnimationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AnimationList.SelectedItem is string name)
            {
                SaveCurrentAnimation(); // store what's currently selected
                LoadAnimation(name);    // switch to new one
            }
        }

        private void SaveCurrentAnimation()
        {
            if (suppressSave || currentAnimation == null)
                return;

            var saved = new Dictionary<int, List<Border>>();
            foreach (var kvp in angleBuffer)
            {
                saved[kvp.Key] = new List<Border>(kvp.Value);
            }
            animationLibrary[currentAnimation].angleToFrames = saved;
            animationLibrary[currentAnimation].reduced_set_flag = ReducedSpriteSet.IsChecked == true ? true : false;
        }

        private void RenameAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (AnimationList.SelectedItem is not string oldName)
            {
                MessageBox.Show("Select an animation to rename.", "Rename Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new RenameDialog(oldName)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.AnimationName.Trim();

                if (string.IsNullOrEmpty(newName))
                {
                    MessageBox.Show("Enter a valid name.", "Rename Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (newName == oldName)
                {
                    // Still save in case contents changed
                    currentAnimation = oldName;
                    SaveCurrentAnimation();
                    return;
                }

                if (animationLibrary.ContainsKey(newName))
                {
                    MessageBox.Show($"\"{newName}\" already exists.", "Rename Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Save current animation BEFORE renaming
                currentAnimation = oldName;
                SaveCurrentAnimation();

                // Copy animation frames
                var savedCopy = new Dictionary<int, List<Border>>();
                var reducedCopy = animationLibrary[oldName].reduced_set_flag;
                foreach (var kvp in animationLibrary[oldName].angleToFrames)
                    savedCopy[kvp.Key] = new List<Border>(kvp.Value);

                animationLibrary.Remove(oldName);
                animationLibrary[newName].angleToFrames = savedCopy;
                animationLibrary[newName].reduced_set_flag = reducedCopy;

                suppressSave = true;

                int index = AnimationList.Items.IndexOf(oldName);
                AnimationList.Items.RemoveAt(index);
                AnimationList.Items.Insert(index, newName);
                AnimationList.SelectedItem = newName;

                suppressSave = false;

                currentAnimation = newName;
                SaveCurrentAnimation();
            }
        }

        private void LoadAnimation(string name)
        {
            ClearAllSelections();
            angleBuffer.Clear();
            currentAnimation = name;

            if (animationLibrary.TryGetValue(name, out var stored))
            {
                foreach (var kvp in stored.angleToFrames)
                {
                    angleBuffer[kvp.Key] = new List<Border>();
                    foreach (var border in kvp.Value)
                    {
                        border.Tag = true;
                        border.BorderBrush = Brushes.Red;
                        angleBuffer[kvp.Key].Add(border);
                    }
                }
                UpdateLabels();
            }
        }

        private void RemoveAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (AnimationList.SelectedItem is not string name)
            {
                MessageBox.Show("Select an animation to remove.", "Remove Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete \"{name}\"?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            animationLibrary.Remove(name);

            int index = AnimationList.Items.IndexOf(name);
            AnimationList.Items.RemoveAt(index);

            // Clear grid if the removed animation was active
            if (currentAnimation == name)
            {
                ClearAllSelections();
                angleBuffer.Clear();
                currentAnimation = null;
                UpdateLabels();
            }

            // Reselect another animation if available
            if (AnimationList.Items.Count > 0)
            {
                AnimationList.SelectedIndex = 0;
            }
        }

        private void ExportAnimationsToJson_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentAnimation(); // Ensure current selection is saved

            var sb = new StringBuilder();
            sb.Append("{");

            var animationNames = animationLibrary.Keys.ToList();

            for (int animIndex = 0; animIndex < animationNames.Count; animIndex++)
            {
                string name = animationNames[animIndex];
                bool reducedFlag = animationLibrary[name].reduced_set_flag;

                var angles = animationLibrary[name].angleToFrames;
                if (animationLibrary[name].reduced_set_flag)
                {
                    var trueAngles = new Dictionary<int, List<Border>>();
                    trueAngles.Add(0, angles[0]);
                    trueAngles.Add(45, angles[45]);
                    trueAngles.Add(90, angles[90]);
                    trueAngles.Add(135, angles[45]);
                    trueAngles.Add(180, angles[0]);
                    trueAngles.Add(225, angles[135]);
                    trueAngles.Add(270, angles[180]);
                    trueAngles.Add(315, angles[135]);

                    angles = trueAngles;
                }

                sb.Append($"\"{name}\":{{\"reduced\":{reducedFlag.ToString().ToLower()},\"animation\":[");

                var angleEntries = new List<string>();

                foreach (var angleEntry in angles.OrderBy(kvp => kvp.Key))
                {
                    int angleIndex = angleEntry.Key / 45;  
                    var frames = angleEntry.Value;

                    var coordList = new List<string>();

                    foreach (var border in frames)
                    {
                        for (int y = 0; y < frameGridData.GetLength(0); y++)
                        {
                            for (int x = 0; x < frameGridData.GetLength(1); x++)
                            {
                                if (frameGridData[y, x] == border)
                                {
                                    coordList.Add($"[{x},{y}]");
                                    goto NextBorder;
                                }
                            }
                        }
                    NextBorder: continue;
                    }

                    string entry = $"[{angleIndex},[{string.Join(",", coordList)}]]";
                    angleEntries.Add(entry);
                }

                sb.Append(string.Join(",", angleEntries));
                sb.Append("]}");

                if (animIndex < animationNames.Count - 1)
                    sb.Append(",");
            }

            sb.Append("}");

            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = "animations.json"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, sb.ToString());
                MessageBox.Show("Export successful!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImportAnimationsFromJson_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Import Animations"
            };

            if (dialog.ShowDialog() != true)
                return;

            string json = File.ReadAllText(dialog.FileName);

            try
            {
                var importedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, AnimationJsonData>>(json);

                animationLibrary.Clear();
                AnimationList.Items.Clear();
                currentAnimation = null;

                foreach (var anim in importedData)
                {
                    string animName = anim.Key;
                    var animData = anim.Value;
                    var angleDict = new Dictionary<int, List<Border>>();

                    foreach (var entry in animData.animation)
                    {
                        int angleIndex = entry[0].GetInt32();
                        var frameList = entry[1].Deserialize<List<List<int>>>();

                        var borders = new List<Border>();
                        foreach (var coords in frameList)
                        {
                            if (coords.Count != 2)
                                continue;

                            int x = coords[0];
                            int y = coords[1];

                            if (y >= 0 && y < frameGridData.GetLength(0) && x >= 0 && x < frameGridData.GetLength(1))
                            {
                                var border = frameGridData[y, x];
                                if (border != null)
                                {
                                    borders.Add(border);
                                }
                            }
                        }

                        angleDict[angleIndex * 45] = borders;
                    }

                    if (animData.reduced)
                    {
                        var trueDict = new Dictionary<int, List<Border>>();

                        trueDict[0] = angleDict[0];
                        trueDict[45] = angleDict[45];
                        trueDict[90] = angleDict[90];
                        trueDict[135] = angleDict[225];
                        trueDict[180] = angleDict[270];
                        angleDict = trueDict;
                    }

                    animationLibrary[animName] = new AnimationData
                    {
                        angleToFrames = angleDict,
                        reduced_set_flag = animData.reduced
                    };

                    AnimationList.Items.Add(animName);
                }

                if (AnimationList.Items.Count > 0)
                    AnimationList.SelectedIndex = 0;

                MessageBox.Show("Import successful!", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import JSON: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}