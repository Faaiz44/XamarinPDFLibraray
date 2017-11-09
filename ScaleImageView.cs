using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace PDFRenderer
{
    public class ScaleImageView : ImageView, View.IOnTouchListener
    {
        private readonly Context mContext;

        private readonly float mMaxScale = 4.0f;

        private Matrix mMatrix;
        private readonly float[] mMatrixValues = new float[9];
        private int mWidth;
        private int mHeight;
        private int mIntrinsicWidth;
        private int mIntrinsicHeight;
        private float mScale;
        private float mMinScale;
        private float mPreviousDistance;
        private int mPreviousMoveX;
        private int mPreviousMoveY;

        private bool mIsScaling;
        private GestureDetector mGestureDetector;

        public ScaleImageView(Context context, IAttributeSet attrs) :
            base(context, attrs)
        {
            mContext = context;
            Initialize();
        }

        public ScaleImageView(Context context, IAttributeSet attrs, int defStyle) :
            base(context, attrs, defStyle)
        {
            mContext = context;
            Initialize();
        }

        public override void SetImageBitmap(Bitmap bm)
        {
            base.SetImageBitmap(bm);
            Initialize();
        }

        public override void SetImageResource(int resId)
        {
            base.SetImageResource(resId);
            Initialize();
        }

        private void Initialize()
        {
            SetScaleType(ScaleType.Matrix);
            mMatrix = new Matrix();

            if (Drawable != null)
            {
                mIntrinsicWidth = Drawable.IntrinsicWidth;
                mIntrinsicHeight = Drawable.IntrinsicHeight;
                SetOnTouchListener(this);
            }

            mGestureDetector = new GestureDetector(mContext, new ScaleImageViewGestureDetector(this));
        }

        protected override bool SetFrame(int l, int t, int r, int b)
        {
            mWidth = r - l;
            mHeight = b - t;

            mMatrix.Reset();
            var rNorm = r - l;
            mScale = rNorm / (float)mIntrinsicWidth;

            var paddingHeight = 0;
            var paddingWidth = 0;
            if (mScale * mIntrinsicHeight > mHeight)
            {
                mScale = mHeight / (float)mIntrinsicHeight;
                mMatrix.PostScale(mScale, mScale);
                paddingWidth = (r - mWidth) / 2;
            }
            else
            {
                mMatrix.PostScale(mScale, mScale);
                paddingHeight = (b - mHeight) / 2;
            }

            mMatrix.PostTranslate(paddingWidth, paddingHeight);
            ImageMatrix = mMatrix;
            mMinScale = mScale;
            Cutting();
            return base.SetFrame(l, t, r, b);
        }

        private float GetValue(Matrix matrix, int whichValue)
        {
            matrix.GetValues(mMatrixValues);
            return mMatrixValues[whichValue];
        }


        private float Scale => GetValue(mMatrix, Matrix.MscaleX);

        private float TranslateX => GetValue(mMatrix, Matrix.MtransX);

        private float TranslateY => GetValue(mMatrix, Matrix.MtransY);

        public void MaxZoomTo(int x, int y)
        {
            if (mMinScale != Scale && Scale - mMinScale > 0.1f)
            {
                var scale = mMinScale / Scale;
                ZoomTo(scale, x, y);
            }
            else
            {
                var scale = mMaxScale / Scale;
                ZoomTo(scale, x, y);
            }
        }

        private void ZoomTo(float scale, int x, int y)
        {
            if (Scale * scale < mMinScale)
            {
                scale = mMinScale / Scale;
            }
            else
            {
                if (scale >= 1 && Scale * scale > mMaxScale)
                {
                    scale = mMaxScale / Scale;
                }
            }
            mMatrix.PostScale(scale, scale);
            //move to center
            mMatrix.PostTranslate(-(mWidth * scale - mWidth) / 2, -(mHeight * scale - mHeight) / 2);

            //move x and y distance
            mMatrix.PostTranslate(-(x - mWidth / 2) * scale, 0);
            mMatrix.PostTranslate(0, -(y - mHeight / 2) * scale);
            ImageMatrix = mMatrix;
        }

        public void Cutting()
        {
            var width = (int)(mIntrinsicWidth * Scale);
            var height = (int)(mIntrinsicHeight * Scale);
            if (TranslateX < -(width - mWidth))
            {
                mMatrix.PostTranslate(-(TranslateX + width - mWidth), 0);
            }

            if (TranslateX > 0)
            {
                mMatrix.PostTranslate(-TranslateX, 0);
            }

            if (TranslateY < -(height - mHeight))
            {
                mMatrix.PostTranslate(0, -(TranslateY + height - mHeight));
            }

            if (TranslateY > 0)
            {
                mMatrix.PostTranslate(0, -TranslateY);
            }

            if (width < mWidth)
            {
                mMatrix.PostTranslate((mWidth - width) / 2, 0);
            }

            if (height < mHeight)
            {
                mMatrix.PostTranslate(0, (mHeight - height) / 2);
            }

            ImageMatrix = mMatrix;
        }

        private float Distance(float x0, float x1, float y0, float y1)
        {
            var x = x0 - x1;
            var y = y0 - y1;
            return FloatMath.Sqrt(x * x + y * y);
        }

        private float DispDistance()
        {
            return FloatMath.Sqrt(mWidth * mWidth + mHeight * mHeight);
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (mGestureDetector.OnTouchEvent(e))
            {
                mPreviousMoveX = (int)e.GetX();
                mPreviousMoveY = (int)e.GetY();
                return true;
            }

            var touchCount = e.PointerCount;
            switch (e.Action)
            {
                case MotionEventActions.Down:
                case MotionEventActions.Pointer1Down:
                case MotionEventActions.Pointer2Down:
                {
                    if (touchCount >= 2)
                    {
                        var distance = Distance(e.GetX(0), e.GetX(1), e.GetY(0), e.GetY(1));
                        mPreviousDistance = distance;
                        mIsScaling = true;
                    }
                }
                    break;

                case MotionEventActions.Move:
                {
                    if (touchCount >= 2 && mIsScaling)
                    {
                        var distance = Distance(e.GetX(0), e.GetX(1), e.GetY(0), e.GetY(1));
                        var scale = (distance - mPreviousDistance) / DispDistance();
                        mPreviousDistance = distance;
                        scale += 1;
                        scale = scale * scale;
                            ZoomTo(scale, mWidth / 2, mHeight / 2);
                            Cutting();
                    }
                    else if (!mIsScaling)
                    {
                        var distanceX = mPreviousMoveX - (int)e.GetX();
                        var distanceY = mPreviousMoveY - (int)e.GetY();
                        mPreviousMoveX = (int)e.GetX();
                        mPreviousMoveY = (int)e.GetY();

                        mMatrix.PostTranslate(-distanceX, -distanceY);
                            Cutting();
                    }
                }
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.Pointer1Up:
                case MotionEventActions.Pointer2Up:
                {
                    if (touchCount <= 1)
                    {
                        mIsScaling = false;
                    }
                }
                    break;
            }
            return true;
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            return OnTouchEvent(e);
        }
    }
}