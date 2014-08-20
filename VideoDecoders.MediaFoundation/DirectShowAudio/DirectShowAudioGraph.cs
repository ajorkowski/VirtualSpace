using DirectShow;
using DirectShow.Helper;
using System.Runtime.InteropServices;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class DirectShowAudioGraph : DSFilterGraphBase
    {
        private readonly MF.IMFMediaType _inputType;
        private readonly MF.IMFMediaType _outputType;

        private AudioSourceFilter _sourceFilter;
        private AudioOutputFilter _outputFilter;

        public DirectShowAudioGraph(MF.IMFMediaType inputType, MF.IMFMediaType outputType)
        {
            _inputType = inputType;
            _outputType = outputType;
        }

        public void PushData(MF.IMFSample sample)
        {
            _sourceFilter.PushData(sample);
        }

        protected override HRESULT OnInitInterfaces()
        {
            // Create Capture Graph Builder
            var capture = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            capture.SetFiltergraph(m_GraphBuilder);

            // Input
            _sourceFilter = new AudioSourceFilter(_inputType);
            var input = new DSBaseSourceFilter(_sourceFilter);
            input.FilterGraph = m_GraphBuilder;

            // Output
            _outputFilter = new AudioOutputFilter(_outputType);
            var output = new DSBaseWriterFilter(_outputFilter);
            output.FilterGraph = m_GraphBuilder;

            // Build capture graph
            HRESULT hr = (HRESULT)capture.RenderStream(null, null, input.Value, null, output.Value);
            Marshal.FinalReleaseComObject(capture);
            return hr;
        }

        protected override HRESULT OnCloseInterfaces()
        {
            return base.OnCloseInterfaces();
        }
    }
}
