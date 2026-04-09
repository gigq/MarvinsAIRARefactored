
const headTitleElement = document.getElementById( 'headTitle' );
const bodyTitleElement = document.getElementById( 'bodyTitle' );
const hintElement = document.getElementById( 'hint' );
const enableElement = document.getElementById( 'enable' );

const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

let speechRecognition = null;
let webSocket = null;
let language = 'en-US';

function post( type, data = {} )
{
    if ( webSocket && webSocket.readyState === WebSocket.OPEN )
    {
        let json = JSON.stringify( { type, ...data } );

        console.log( json );

        webSocket.send( json );
    }
}

function wsUrl()
{
    const { protocol, host } = window.location;

    return ( protocol === 'https:' ? 'wss:' : 'ws:' ) + '//' + host + '/ws';
}

let strings = {
    title: 'MALMUA STT Bridge',
    hint: 'Click the enable button. You must check that the browser has the correct audio device selected, by clicking on  the microphone icon to the left of the address bar!',
    button: 'Enable Speech-To-Text'
};

function applyStrings()
{
    headTitleElement.textContent = strings.title;
    bodyTitleElement.textContent = strings.title;
    hintElement.textContent = strings.hint;
    enableElement.textContent = strings.button;
}

function ensureWs()
{
    if ( webSocket && ( webSocket.readyState === WebSocket.OPEN || webSocket.readyState === WebSocket.CONNECTING ) ) return;

    webSocket = new WebSocket( wsUrl() );

    webSocket.onclose = () =>
    {
        setTimeout( ensureWs, 1000 );
    };

    webSocket.onmessage = ( ev ) =>
    {
        try
        {
            const msg = JSON.parse( ev.data );

            console.log( msg );

            if ( msg.type === 'setlanguage' )
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
                destroyRecognizer();

                window.close();
            }
        }
        catch
        {
        }
    };
}

ensureWs();

function createAndStartRecognizer()
{
    if ( !SpeechRecognition )
    {
        post( 'error', { message: 'SpeechRecognition not supported in this browser.' } );

        return null;
    }

    destroyRecognizer();

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

        if ( partial )
        {
            post( 'stt', { partial } );
        }

        if ( final )
        {
            post( 'stt', { final } );
        }
    };

    speechRecognition.onerror = ( ev ) => post( 'error', { message: ev.error || 'unknown' } );

    speechRecognition.onend = () =>
    {
        post( 'end' );

        setTimeout( () => { try { speechRecognition.start(); } catch { } }, 60 );
    };

    try
    {
        speechRecognition.start();
    }
    catch ( e )
    {
        if ( !String( e ).includes( 'start' ) )
        {
            post( 'error', { message: e.message || 'start() failed' } );
        }
    }

    enableElement.classList.add( 'hidden' );
}

function destroyRecognizer()
{
    if ( speechRecognition )
    {
        try
        {
            speechRecognition.abort();
        }
        catch
        {
        }

        speechRecognition = null;
    }
}

function applyLanguage( newLanguage )
{
    if ( !newLanguage || ( newLanguage === language ) ) return;

    language = newLanguage;

    createAndStartRecognizer();
}

enableElement.addEventListener( 'click', async () =>
{
    try
    {
        createAndStartRecognizer();
    }
    catch ( e )
    {
    }
} );
