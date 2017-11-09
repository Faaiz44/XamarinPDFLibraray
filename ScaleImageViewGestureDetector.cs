using Android.Views;

namespace PDFRenderer
{
    public class ScaleImageViewGestureDetector : GestureDetector.SimpleOnGestureListener
    {
        private readonly ScaleImageView mScaleImageView;
        public ScaleImageViewGestureDetector(ScaleImageView imageView)
        {
            mScaleImageView = imageView;
        }

        public override bool OnDown(MotionEvent e)
        {
            return true;
        }

        public override bool OnDoubleTap(MotionEvent e)
        {
            int temp = (int)e.GetY();
            mScaleImageView.MaxZoomTo((int)e.GetX(), (int)e.GetY());
            mScaleImageView.Cutting();
            return true;
        }
    }
}