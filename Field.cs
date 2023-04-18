using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Windows.Shapes;
using FluidSim.Modules;

namespace FluidSim
{
    public class Field
    {
        Cell empty;
        Cell[,] grid;
        int width, height;

        DispatcherTimer step_timer;
        Line[,] vectors;

        static double time_step = 0.1, k_dense_step = 1.0, k_vel_step = 1.0;
        static int divergence_iters = 10;

        public Field(int width, int height, Rect size, Canvas parent, int delay = 60)
        {
            this.width = width;
            this.height = height;
            grid = new Cell[height, width];
            vectors = new Line[height, width];
            double cell_size = size.Width / width;
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; x++)
                {
                    grid[y, x] = new Cell(this, cell_size, new Point(size.X + cell_size * x, size.Y + cell_size * y), parent, x, y);
                    double offset_x = Canvas.GetLeft(grid[y, x].UICell) + grid[y, x].UICell.Width / 2.0,
                           offset_y = Canvas.GetTop(grid[y, x].UICell) + grid[y, x].UICell.Height / 2.0;
                    vectors[y, x] = new Line
                    {
                        Stroke = Brushes.White,
                        StrokeThickness = 1.0,
                        Visibility = Visibility.Hidden,
                        X1 = offset_x,
                        Y1 = offset_y,
                        X2 = offset_x,
                        Y2 = offset_y
                    };
                    parent.Children.Add(vectors[y, x]);
                }
            }

            empty = new Cell();
            empty.Density = 30.0;
            empty.Velocity = new Vector(15.0, 0.0);

            step_timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, delay)
            };
            step_timer.Tick += Step;
        }

        public void Init()
        {
            Random rand = new Random();
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; x++)
                {
                    if (10 <= x && x <= 20 && 40 <= y && y <= 60) {
                        if (y <= 50)
                            grid[y, x].Velocity = new Vector(0, -10);
                        else
                            grid[y, x].Velocity = new Vector(0, 10);
                        grid[y, x].Density = 0.0;
                        continue;
                    }
                    grid[y, x].Density = empty.Density;
                    grid[y, x].Velocity = empty.Velocity;
                }
            }
        }

        public void BeginSimulation()
        {
            step_timer.Start();
        }

        public void PauseSimulation()
        {
            step_timer.Stop();
        }

        private void Step(object sender, EventArgs e)
        {
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; x++)
                {
                    if (10 <= x && x <= 20 && 40 <= y && y <= 60)
                    {
                        continue;
                    }
                    grid[y, x].CalcDiffusion(time_step, k_dense_step, k_vel_step);
                }
            }
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; x++)
                {
                    if (10 <= x && x <= 20 && 40 <= y && y <= 60)
                    {
                        continue;
                    }
                    grid[y, x].UpdateParams();
                }
            }

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; x++)
                {
                    if (10 <= x && x <= 20 && 40 <= y && y <= 60)
                    {
                        continue;
                    }
                    grid[y, x].CalcAdvection(time_step);
                }
            }
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; x++)
                {
                    if (10 <= x && x <= 20 && 40 <= y && y <= 60)
                    {
                        continue;
                    }
                    grid[y, x].UpdateParams();
                }
            }

            for (int l = 0; l < divergence_iters; ++l)
            {
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (10 <= x && x <= 20 && 40 <= y && y <= 60)
                        {
                            continue;
                        }
                        grid[y, x].ClearDivergence();
                    }
                }

                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (10 <= x && x <= 20 && 40 <= y && y <= 60)
                        {
                            continue;
                        }
                        grid[y, x].UpdateDivergence();
                    }
                }
            }

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; x++)
                {
                    if (10 <= x && x <= 20 && 40 <= y && y <= 60)
                    {
                        continue;
                    }
                    grid[y, x].UpdateUI();
                    //vectors[y, x].X2 = vectors[y, x].X1 + grid[y, x].Velocity.X;
                    //vectors[y, x].Y2 = vectors[y, x].Y1 + grid[y, x].Velocity.Y;
                }
            }
        }

        public Cell this[int y, int x]
        {
            get {
                if (0 <= y && y < height && 0 <= x && x < width)
                {
                    return grid[y, x];
                }
                else
                {
                    return empty;
                }
            }
        }
    }

    public class Cell
    {
        double density;
        Vector velocity;
        double potential = 0.0;

        double new_density;
        Vector new_velocity;
        double new_potential;

        int x, y;
        Field field;

        public Rectangle UICell;

        static double VisualSizeOffset = 0.1, VisualDensityParam = 0.1, VisualVelocityParam = 80.0;

        public static double Saturate(double x, double threshold)
        {
            return threshold * (1.0 - Math.Exp(-x / threshold));
        }

        public static double Lerp(double from, double to, double pos)
        {
            return from + pos * (to - from);
        }

        public static Vector Lerp(Vector from, Vector to, double pos)
        {
            return from + pos * (to - from);
        }

        public Cell()
        {
            this.velocity = default(Vector);
            this.density = 0.0;
        }

        public Cell(Field field, double uisize, Point uicoords, Canvas parent, int x, int y, Vector velocity = default(Vector), double density = 0.0)
        {
            this.field = field;
            this.velocity = velocity;
            this.density = density;
            this.x = x;
            this.y = y;
            UICell = new Rectangle
            {
                Width = uisize + VisualSizeOffset * 2.0,
                Height = uisize + VisualSizeOffset * 2.0,
                Fill = GetBrush()
            };
            Canvas.SetLeft(UICell, uicoords.X - VisualSizeOffset);
            Canvas.SetTop(UICell, uicoords.Y - VisualSizeOffset);
            parent.Children.Add(UICell);
        }

        public void CalcDiffusion(double dt, double k_dense, double k_vel)
        {
            new_density = (density + dt * k_dense * (field[y, x + 1].density + field[y, x - 1].density + field[y + 1, x].density + field[y - 1, x].density) / 4.0) / (1.0 + dt * k_dense);
            new_velocity = (velocity + dt * k_vel * (field[y, x + 1].velocity + field[y, x - 1].velocity + field[y + 1, x].velocity + field[y - 1, x].velocity) / 4.0) / (1.0 + dt * k_vel);
        }

        public void CalcAdvection(double dt)
        {
            Point prev_pos = new Point(x, y) - velocity * dt;
            int floor_x = (int)Math.Floor(prev_pos.X),
                floor_y = (int)Math.Floor(prev_pos.Y);
            double fract_x = prev_pos.X - floor_x,
                   fract_y = prev_pos.Y - floor_y;
            new_density = Lerp(
                Lerp(field[floor_y, floor_x].density, field[floor_y, floor_x + 1].density, fract_x),
                Lerp(field[floor_y + 1, floor_x].density, field[floor_y + 1, floor_x + 1].density, fract_x),
                fract_y
            );
            new_velocity = Lerp(
                Lerp(field[floor_y, floor_x].velocity, field[floor_y, floor_x + 1].velocity, fract_x),
                Lerp(field[floor_y + 1, floor_x].velocity, field[floor_y + 1, floor_x + 1].velocity, fract_x),
                fract_y
            );
        }

        public void UpdateParams()
        {
            density = new_density;
            velocity = new_velocity;
        }

        public void ClearDivergence()
        {
            new_potential = (field[y, x - 1].potential + field[y, x + 1].potential +
                             field[y - 1, x].potential + field[y + 1, x].potential - Grad()) / 4.0;
        }

        public void UpdateDivergence()
        {
            potential = new_potential;
        }

        public void UpdateUI()
        {
            new_velocity = new Vector((field[y, x + 1].potential - field[y, x - 1].potential) / 2.0, (field[y + 1, x].potential - field[y - 1, x].potential) / 2.0);
            velocity -= new_velocity;

            UICell.Fill = GetBrush();
        }

        public Brush GetBrush()
        {
            double hue = Saturate(Speed() * VisualVelocityParam, 255.0);
            //hue = 220;
            double alpha = Saturate(density * VisualDensityParam, 1.0);
            return new SolidColorBrush(ColorConversion.FromHSV(hue, 1.0, 0.9, alpha));
        }

        public double Speed()
        {
            return Math.Sqrt(Math.Pow(velocity.X, 2) + Math.Pow(velocity.Y, 2));
        }

        public double Grad()
        {
            return (field[y, x + 1].velocity.X - field[y, x - 1].velocity.X +
                    field[y + 1, x].velocity.Y - field[y - 1, x].velocity.Y) / 2.0;
        }

        public double Density { get => density; set => density = value; }
        public Vector Velocity { get => velocity; set => velocity = value; }
    }
}
