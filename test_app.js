let wsCalled = false;
global.WebSocket = class {
    constructor(url) {
        console.log('WebSocket constructor called with:', url);
        wsCalled = true;
    }
};
global.window = global;
global.addEventListener = () => {};
const elementStub = { 
    addEventListener: () => {}, 
    getContext: () => ({ 
        fillRect: () => {}, 
        clearRect: () => {}, 
        fillText: () => {},
        measureText: () => ({ width: 0 })
    }),
    classList: { toggle: () => {}, add: () => {}, remove: () => {} },
    style: {},
    getBoundingClientRect: () => ({ width: 800, height: 600, top: 0, left: 0 })
};
global.document = {
    addEventListener: () => {},
    getElementById: () => elementStub,
    querySelector: () => elementStub,
    createElement: () => elementStub
};
global.navigator = { userAgent: 'node' };
global.location = { href: 'http://localhost' };
global.Image = class {};
global.requestAnimationFrame = () => {};

try {
    require('./Web/app.js');
    console.log('Execution successful');
} catch (e) {
    console.error('Execution threw error:', e.stack);
}
console.log('WebSocket called:', wsCalled);
