using DirectShow;
using DirectShow.BaseClasses;
using DirectShow.Helper;
using System;
using System.Runtime.InteropServices;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class DirectShowAudioGraph : DSFilterGraphBase
    {
        private readonly MF.IMFMediaType _inputType;
        private readonly MF.IMFMediaType _outputType;

        public DirectShowAudioGraph(MF.IMFMediaType inputType, MF.IMFMediaType outputType)
        {
            _inputType = inputType;
            _outputType = outputType;
        }

        protected override HRESULT OnInitInterfaces()
        {
            // Create Capture Graph Builder
            var capture = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            capture.SetFiltergraph(m_GraphBuilder);

            // Input
            var input = new DSBaseSourceFilter(new AudioSourceFilter(_inputType));
            input.FilterGraph = m_GraphBuilder;

            // Output
            var output = new DSBaseWriterFilter(new AudioOutputFilter(_outputType));
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
