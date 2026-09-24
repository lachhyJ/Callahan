import Toybox.Application;
import Toybox.Lang;
import Toybox.WatchUi;

class CallahanDataFieldApp extends Application.AppBase {

    function initialize() {
        AppBase.initialize();
    }

    function onStart(state as Dictionary?) as Void {
    }

    function onStop(state as Dictionary?) as Void {
    }

    function getInitialView() as [Views] or [Views, InputDelegates] {
        return [ new CallahanDataField() ];
    }
}

function getApp() as CallahanDataFieldApp {
    return Application.getApp() as CallahanDataFieldApp;
}
