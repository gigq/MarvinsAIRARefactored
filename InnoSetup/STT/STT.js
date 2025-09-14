
const statusElement = document.getElementById( 'status' );
const enableElement = document.getElementById( 'enable' );
const micSelectElement = document.getElementById( 'micSelect' );
const disconnectedElement = document.getElementById( 'disconnected' );
const waitingElement = document.getElementById( 'waiting' );
const listeningElement = document.getElementById( 'listening' );

const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

let speechRecognition = null;
let webSocket = null;
let language = 'en-US';
let isPressed = false;

let mediaStream = null;
let micDeviceId = localStorage.getItem( 'maira.micDeviceId' ) || '';

let audioContext = null;
let sourceNode = null;

function post( type, data = {} ) { if ( webSocket && webSocket.readyState === WebSocket.OPEN ) webSocket.send( JSON.stringify( { type, ...data } ) ); }
function wsUrl() { const { protocol, host } = window.location; return ( protocol === 'https:' ? 'wss:' : 'ws:' ) + '//' + host + '/ws'; }

let strings = {
    title: "MAIRA STT Bridge",
    hint: "Choose an audio capture device, then click the enable button.",
    button: "Enable Speech-To-Text"
};

function applyStrings()
{
    const headTitleElement = document.getElementById( 'headTitle' );
    const bodyTitleElement = document.getElementById( 'bodyTitle' );
    const hintElement = document.getElementById( 'hint' );

    headTitleElement.textContent = strings.title;
    bodyTitleElement.textContent = strings.title;
    hintElement.textContent = strings.hint;
    enableElement.textContent = strings.button;
}

function setStatus( which )
{
    disconnectedElement.classList.add( 'hidden' );
    waitingElement.classList.add( 'hidden' );
    listeningElement.classList.add( 'hidden' );

    switch ( which )
    {
        case 0:
            disconnectedElement.classList.remove( 'hidden' );
            break;

        case 1:
            waitingElement.classList.remove( 'hidden' );
            break;

        case 2:
            listeningElement.classList.remove( 'hidden' );
            break;
    }
}

function ensureWs()
{
    if ( webSocket && ( webSocket.readyState === WebSocket.OPEN || webSocket.readyState === WebSocket.CONNECTING ) ) return;

    webSocket = new WebSocket( wsUrl() );

    webSocket.onopen = () =>
    {
        setStatus( 0 );
        refreshDevices();
    };

    webSocket.onclose = () =>
    {
        setStatus( 0 );
        setTimeout( ensureWs, 1000 );
    };

    webSocket.onmessage = ( ev ) =>
    {
        try
        {
            const msg = JSON.parse( ev.data );

            if ( msg.type === 'start' )
            {
                startRecognition();
            }
            else if ( msg.type === 'stop' )
            {
                stopRecognition();
            }
            else if ( msg.type === 'setlanguage' )
            {
                if ( msg.language )
                {
                    applyLanguage( msg.language );
                }
            }
            else if ( msg.type === 'setstrings' )
            {
                strings = { ...strings, ...msg.strings };

                applyStrings();
            }
            else if ( msg.type === 'shutdown' )
            {
                stopRecognition();
                stopMediaStream();

                setStatus( 0 );

                window.close();
            }
        }
        catch
        {
        }
    };
}

ensureWs();

async function refreshDevices()
{
    try
    {
        await navigator.mediaDevices.getUserMedia( { audio: true } );
    }
    catch
    {
    }

    const devices = await navigator.mediaDevices.enumerateDevices();
    const mics = devices.filter( d => d.kind === 'audioinput' );

    micSelectElement.innerHTML = '';

    for ( const d of mics )
    {
        const opt = document.createElement( 'option' );

        opt.value = d.deviceId;
        opt.text = d.label || ( 'Microphone ' + ( micSelectElement.length + 1 ) );

        if ( micDeviceId && d.deviceId === micDeviceId )
        {
            opt.selected = true;
        }

        micSelectElement.appendChild( opt );
    }

    if ( !micDeviceId && mics.length )
    {
        micDeviceId = mics[ 0 ].deviceId;
    }
}

navigator.mediaDevices.addEventListener( 'devicechange', refreshDevices );

async function switchMic( deviceId )
{
    micDeviceId = deviceId || '';

    localStorage.setItem( 'maira.micDeviceId', micDeviceId );

    stopMediaStream();

    enableElement.classList.remove( 'hidden' )

    try
    {
        const constraints = micDeviceId ? { audio: { deviceId: { exact: micDeviceId } } } : { audio: true };

        mediaStream = await navigator.mediaDevices.getUserMedia( constraints );

        setStatus( 0 );
    }
    catch ( e )
    {
        setStatus( 0 );
    }
}

micSelectElement.addEventListener( 'change', () => switchMic( micSelectElement.value ) );

function stopMediaStream()
{
    if ( mediaStream )
    {
        for ( const t of mediaStream.getTracks() )
        {
            t.stop();
        }

        mediaStream = null;
    }
}

function createRecognizer()
{
    if ( !SpeechRecognition )
    {
        post( 'error', { message: 'SpeechRecognition not supported in this browser.' } );

        return null;
    }

    if ( speechRecognition )
    {
        try
        {
            speechRecognition.abort();
        }
        catch
        {
        }
    }

    speechRecognition = new SpeechRecognition();

    speechRecognition.lang = language;
    speechRecognition.interimResults = true;
    speechRecognition.continuous = true;
    speechRecognition.maxAlternatives = 1;

    speechRecognition.onresult = ( e ) =>
    {
        let partial = '', final = '';

        for ( let i = e.resultIndex; i < e.results.length; i++ )
        {
            const r = e.results[ i ];

            if ( r.isFinal )
            {
                final += r[ 0 ].transcript;
            }
            else
            {
                partial += r[ 0 ].transcript;
            }
        }

        if ( partial ) post( 'stt', { partial } );
        if ( final ) post( 'stt', { final } );
    };

    speechRecognition.onerror = ( ev ) => post( 'error', { message: ev.error || 'unknown' } );

    speechRecognition.onend = () =>
    {
        post( 'end' );

        if ( isPressed )
        {
            setTimeout( () => { try { speechRecognition.start(); } catch { } }, 60 );
        }
        else
        {
            setStatus( 1 );
        }
    };

    enableElement.classList.add( 'hidden' )

    return speechRecognition;
}

function startRecognition()
{
    isPressed = true;

    if ( !speechRecognition )
    {
        createRecognizer();
    }

    if ( !mediaStream )
    {
        navigator.mediaDevices.getUserMedia( { audio: true } ).then( s => { mediaStream = s; } ).catch( () => { } );
    }

    try
    {
        speechRecognition.start();

        setStatus( 2 );
    }
    catch ( e )
    {
        if ( !String( e ).includes( 'start' ) )
        {
            post( 'error', { message: e.message || 'start() failed' } );
        }
    }
}

function stopRecognition()
{
    isPressed = false;

    try
    {
        speechRecognition && speechRecognition.stop();
    }
    catch
    {
    }

    setStatus( 1 );
}

function applyLanguage( newLanguage )
{
    if ( !newLanguage || ( newLanguage === language ) )
    {
        return;
    }

    language = newLanguage;

    const wasRunning = isPressed;

    try
    {
        speechRecognition && speechRecognition.abort();
    }
    catch
    {
    }

    createRecognizer();

    if ( wasRunning )
    {
        try
        {
            speechRecognition.start();
        }
        catch
        {
        }
    }
}

enableElement.addEventListener( 'click', async () =>
{
    try
    {
        await switchMic( micSelectElement.value );

        createRecognizer();

        speechRecognition.start();
        speechRecognition.stop();

        setStatus( 1 );
    }
    catch ( e )
    {
        setStatus( 0 );
    }
} );

document.addEventListener( 'DOMContentLoaded', refreshDevices );
