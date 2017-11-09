using System;
using System.Net;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace PDFRenderer
{
    public class PdfRendererView : LinearLayout
    {
        public static string PdfFileName = "thePDFDocument.pdf";
        public static string PdfFolder = "/.tempPDF";
        public ImageButton goForward;
        public ImageButton goBack;
        private Bitmap bitmapPdf;
        private int currentPage;
        private Java.IO.File pdfFile;
        private PdfRenderer pdfRenderer;
        Rect rect;

        private ScaleImageView pdfView;
        public TextView pageCount;

        private string documentsPath;
        private bool rendering = false;
        private string pdfFilePath;
        private string pdfUrl;
        private readonly WebClient webClient = new WebClient();

        private RelativeLayout pdfDocumentRelativeLayout;
        private TextView errorMessageTextView;
        private int totalPages;
        private Context context;
        private ProgressBar progressBar;
        public PdfRendererView(Context context) : base(context)
        {
            this.context = context;
            SetContentView(context, Resource.Layout.DocumentView);
            InitComponents(this);
        }

        public PdfRendererView(Context context, IAttributeSet attrs): base(context,attrs)
        {
            SetContentView(context, Resource.Layout.DocumentView);
            InitComponents(this);
        }

        protected override void OnDetachedFromWindow()
        {
            base.OnDetachedFromWindow();
            DeleteReport(pdfFilePath);
        }

        /// <summary>
        /// Loads the document from URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        public void LoadDocumentFromUrl(string url)
        {
            progressBar.Visibility = ViewStates.Visible;
            if (AndroidVersionCheck())
            {
                pdfUrl = url;
                DownloadPdfDocument();
            }
            else
            {
                pdfDocumentRelativeLayout.Visibility = ViewStates.Gone;
                errorMessageTextView.Visibility = ViewStates.Visible;
            }
        }

        /// <summary>
        /// Loads the document from file.
        /// </summary>
        /// <param name="path">The path.</param>
        public void LoadDocumentFromFile(string path)
        {
            pdfFilePath = path;
            progressBar.Visibility = ViewStates.Visible;
            LoadDocument(path);
        }
        private bool AndroidVersionCheck()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                return true;
            return false;
        }
        /// <summary>
        /// Sets the content view.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="resourceId">The resource identifier.</param>
        protected void SetContentView(Context context, int resourceId)
        {
            var inflater = LayoutInflater.From(context);
            inflater.Inflate(resourceId, this);
        }
        /// <summary>
        /// Inits the components.
        /// </summary>
        /// <param name="view">View.</param>
        protected void InitComponents(View view)
        {
            pdfView = view.FindViewById<ScaleImageView>(Resource.Id.pdfView);
            pageCount = view.FindViewById(Resource.Id.pageCount) as TextView;
            goForward = view.FindViewById<ImageButton>(Resource.Id.forwardButton);
            goBack = view.FindViewById<ImageButton>(Resource.Id.backButton);
            goForward.Visibility = ViewStates.Gone;
            goBack.Visibility = ViewStates.Gone;
            goForward.Click += GoForwardOnClick;
            goBack.Click += GoBackOnClick;

            pdfDocumentRelativeLayout = view.FindViewById<RelativeLayout>(Resource.Id.documentRelativeView);
            errorMessageTextView = view.FindViewById<TextView>(Resource.Id.documentViewMessage);
            progressBar = view.FindViewById<ProgressBar>(Resource.Id.progressBar);
            progressBar.Indeterminate = true;
        }
        private void GoBackOnClick(object sender, EventArgs eventArgs)
        {
            Render(currentPage - 1);
        }

        private void GoForwardOnClick(object sender, EventArgs eventArgs)
        {
            Render(currentPage + 1);
        }
        private async void Render(int page, bool isFirstTime = false)
        {
            if (pdfFile.Exists())
            {
                try
                {
                    rendering = true;
                    pdfRenderer = new PdfRenderer(ParcelFileDescriptor.Open(pdfFile, ParcelFileMode.ReadOnly));
                    totalPages = pdfRenderer.PageCount;
                    var previousPage = currentPage;
                    currentPage = page;
                    if (currentPage < 0)
                    {
                        currentPage = 0;
                    }
                    else if (currentPage >= pdfRenderer.PageCount)
                    {
                        currentPage = pdfRenderer.PageCount - 1;
                    }
                    if (currentPage == previousPage && !isFirstTime)
                    {
                        return;
                    }
                    pageCount.Text = $"{currentPage + 1} of {pdfRenderer.PageCount}";
                    var pdfPage = pdfRenderer.OpenPage(currentPage);
                    setVisibilities(false);
                    await
                        Task.Run(
                            () =>
                                pdfPage.Render(GetBitmap(pdfPage.Width, pdfPage.Height), rect, pdfView.Matrix,
                                    PdfRenderMode.ForDisplay));
                    pdfView.SetImageBitmap(bitmapPdf);
                    pdfView.Invalidate();
                    pdfRenderer.Dispose();
                    pdfRenderer = null;
                    bitmapPdf = null;
                    GC.Collect();
                    setVisibilities(true);
                    rendering = false;
                }
                catch (Exception ex)
                {
                    // ignored
                }
            }
            else
            {
                if (page < 0)
                    currentPage = 0;
                else if (page < totalPages)
                    currentPage = page;
                DownloadPdfDocument();
            }
        }
        private void DownloadPdfDocument()
        {
            documentsPath = Context.CacheDir.AbsolutePath + PdfFolder;

            pdfFilePath = System.IO.Path.Combine(documentsPath, PdfFileName);

            if (!System.IO.Directory.Exists(documentsPath))
            {
                System.IO.Directory.CreateDirectory(documentsPath);
            }
            else
            {
                DeleteReport(pdfFilePath);
            }

            webClient.DownloadDataCompleted += OnPdfDownloadCompleted;

            var url = new Uri(pdfUrl);
            try
            {
                webClient.DownloadDataAsync(url);
            }
            catch (Exception ex)
            {

            }

        }

        private void OnPdfDownloadCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                var pdfBytes = e.Result;
                System.IO.File.WriteAllBytes(pdfFilePath, pdfBytes);
                LoadDocument(pdfFilePath);
            }
        }

        private void LoadDocument(string path)
        {
            pdfFile = new Java.IO.File(path);
            goForward.Visibility = ViewStates.Visible;
            goBack.Visibility = ViewStates.Visible;
            if (!rendering)
                Render(currentPage, true);
            progressBar.Visibility = ViewStates.Gone;
        }

        public static void DeleteReport(string finalPath)
        {
            //string pdfFilePath = path + PdfFolder;
            //string finalPath = System.IO.Path.Combine(pdfFilePath, PdfFileName);
            if (System.IO.File.Exists(finalPath))
            {
                System.IO.File.Delete(finalPath);
            }
        }
        private void setVisibilities(bool visibility)
        {
            goForward.Clickable = visibility;
            goForward.Enabled = visibility;
            goBack.Clickable = visibility;
            goBack.Enabled = visibility;
            if (!visibility)
            progressBar.Visibility = ViewStates.Visible;
            else
            progressBar.Visibility = ViewStates.Gone;
        }
        private Bitmap GetBitmap(int width, int height)
        {
            bitmapPdf = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb4444);
            rect = new Rect(0, 0, width, height);
            return bitmapPdf;
        }

        protected override void Dispose(bool disposing)
        {
            DeleteReport(pdfFilePath);
            base.Dispose(disposing);
        }
    }
}
