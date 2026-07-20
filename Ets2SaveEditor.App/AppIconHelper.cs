using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ets2SaveEditor.App
{
    /// <summary>
    /// Renders the in-app truck Path into a bitmap icon (window / taskbar).
    /// </summary>
    internal static class AppIconHelper
    {
        public const string TruckPathData =
            "M20 8h-3V4H3c-1.1 0-2 .9-2 2v11h2c0 1.66 1.34 3 3 3s3-1.34 3-3h6c0 1.66 1.34 3 3 3s3-1.34 3-3h2v-5l-3-4zm-3.5 3.5h-2.5V8.5H18v3z";

        /// <summary>Neutral gray for the static Explorer .ico.</summary>
        public static readonly Color ExplorerGray = Color.FromRgb(0x8A, 0x93, 0xA5);

        public static ImageSource Create(Color fill, int size = 64)
        {
            if (size < 16) size = 16;

            double pad = size * 0.12;
            double draw = size - pad * 2;
            var geometry = Geometry.Parse(TruckPathData);
            geometry.Freeze();
            Rect bounds = geometry.Bounds;
            double scale = Math.Min(draw / bounds.Width, draw / bounds.Height);

            var brush = new SolidColorBrush(fill);
            brush.Freeze();

            var group = new DrawingGroup();
            using (DrawingContext ctx = group.Open())
            {
                ctx.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));
                double ox = (size - bounds.Width * scale) / 2.0 - bounds.X * scale;
                double oy = (size - bounds.Height * scale) / 2.0 - bounds.Y * scale;
                ctx.PushTransform(new MatrixTransform(scale, 0, 0, scale, ox, oy));
                ctx.DrawGeometry(brush, null, geometry);
                ctx.Pop();
            }

            var drawing = new DrawingImage(group);
            drawing.Freeze();

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
                dc.DrawImage(drawing, new Rect(0, 0, size, size));

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
    }
}
