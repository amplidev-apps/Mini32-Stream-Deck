"""End-to-end local engine protocol tests. Uses a fake OBS; never controls the real OBS."""
import base64
import hashlib
import json
import pathlib
import queue
import socket
import struct
import subprocess
import tempfile
import threading
import time

APP = pathlib.Path(__file__).resolve().parent

class FakeObs:
    def __init__(self):
        self.server = socket.socket()
        self.server.bind(('127.0.0.1', 0))
        self.server.listen()
        self.port = self.server.getsockname()[1]
        self.requests = []
        self.errors = []
        self.thread = threading.Thread(target=self.run, daemon=True)
        self.thread.start()

    def run(self):
        try:
            conn, _ = self.server.accept()
            with conn:
                conn.settimeout(15)
                def exact(n):
                    data = b''
                    while len(data) < n:
                        part = conn.recv(n-len(data))
                        if not part: raise EOFError()
                        data += part
                    return data
                def receive():
                    header = exact(2)
                    if header[0] & 15 == 8: raise EOFError()
                    n = header[1] & 127
                    if n == 126: n = struct.unpack('!H', exact(2))[0]
                    if n == 127: n = struct.unpack('!Q', exact(8))[0]
                    mask = exact(4) if header[1] & 128 else None
                    payload = exact(n)
                    if mask: payload = bytes(v ^ mask[i % 4] for i,v in enumerate(payload))
                    return json.loads(payload)
                def send(value):
                    data = json.dumps(value).encode()
                    head = bytes([129,len(data)]) if len(data) < 126 else bytes([129,126])+struct.pack('!H',len(data))
                    conn.sendall(head+data)
                header = b''
                while not header.endswith(b'\r\n\r\n'): header += exact(1)
                key = next(line.split(b':',1)[1].strip() for line in header.split(b'\r\n') if line.lower().startswith(b'sec-websocket-key:'))
                accept = base64.b64encode(hashlib.sha1(key+b'258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest())
                conn.sendall(b'HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: '+accept+b'\r\n\r\n')
                send({'op':0,'d':{'rpcVersion':1,'authentication':{'salt':'salt','challenge':'challenge'}}})
                auth = receive()['d']['authentication']
                secret = base64.b64encode(hashlib.sha256(b'test-passwordsalt').digest()).decode()
                assert auth == base64.b64encode(hashlib.sha256((secret+'challenge').encode()).digest()).decode()
                send({'op':2,'d':{'negotiatedRpcVersion':1}})
                while True:
                    request = receive()['d']
                    name = request['requestType']
                    self.requests.append(name)
                    data = {}
                    if name == 'GetSceneList': data = {'currentProgramSceneName':'Principal','scenes':[{'sceneName':'Principal'},{'sceneName':'Pausa'}]}
                    if name == 'GetInputList': data = {'inputs':[{'inputName':'Microfone'}]}
                    send({'op':7,'d':{'requestId':request['requestId'],'requestStatus':{'result':True,'code':100},'responseData':data}})
        except (EOFError, ConnectionResetError): pass
        except Exception as exc: self.errors.append(str(exc))
        finally: self.server.close()

class Host:
    def __init__(self, root):
        self.process = subprocess.Popen([str(APP/'bin/Mini32.Engine.exe'),'--data-dir',str(root)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',creationflags=subprocess.CREATE_NO_WINDOW)
        self.events = queue.Queue()
        self.config = {}
        self.sequence = 0
        def read():
            for line in self.process.stdout:
                self.events.put(json.loads(line))
        threading.Thread(target=read,daemon=True).start()
        self.until(lambda m:m['type']=='config')
    def until(self, predicate, timeout=10):
        deadline=time.monotonic()+timeout
        while time.monotonic()<deadline:
            msg=self.events.get(timeout=max(0.01,deadline-time.monotonic()))
            if msg['type']=='config':self.config=msg['data']
            if msg['type']=='fatal':raise AssertionError(msg)
            if predicate(msg):return msg
        raise TimeoutError('No reply')
    def command(self, op, **args):
        self.sequence+=1
        self.process.stdin.write(json.dumps({'id':self.sequence,'op':op,**args})+'\n');self.process.stdin.flush()
        reply=self.until(lambda m:m['type']=='reply' and m['data']['id']==self.sequence)['data']
        assert reply['ok'],reply
        return reply.get('result')
    def rejected(self, op, **args):
        self.sequence+=1
        self.process.stdin.write(json.dumps({'id':self.sequence,'op':op,**args})+'\n');self.process.stdin.flush()
        reply=self.until(lambda m:m['type']=='reply' and m['data']['id']==self.sequence)['data']
        assert not reply['ok'],reply
        return reply['error']
    def close(self):
        if self.process.poll() is None:
            self.command('exit');self.process.wait(5)

def check(value, name):
    assert value,name
    print('PASS:',name,flush=True)

def main():
    root=pathlib.Path(tempfile.mkdtemp(prefix='Mini32-protocol-'))
    host=Host(root)
    try:
        check(len(host.config['Banks'][0]['Keys'])==32,'32 independent keys')
        meter=host.until(lambda m:m['type']=='meter')['data']
        check(len(meter['bars'])==48 and meter['playing']==0,'idle audio meter protocol')
        check(len(host.config['FreeKeys'])==16,'free has 16 selected positions')
        check('Pro' in host.rejected('newBank',name='Entrevista'),'free cannot create a second bank')
        host.command('renameBank',name='Conversa')
        bank=host.config['ActiveBank']
        asset=host.command('example')
        check((root/'library'/asset['File']).exists(),'audio copied into owned library')
        host.command('action',index=0,volume=40,action={'Name':'Meu som','Kind':'audio','AssetId':asset['Id'],'Volume':25,'Pitch':True})
        action=host.config['Banks'][0]['Keys'][0]['ActionId']
        host.command('share',index=1,actionId=action)
        check(host.config['Banks'][0]['Keys'][1]['ActionId']==action,'shared action identity')
        host.command('keyVolume',index=1,volume=12)
        check(host.config['Banks'][0]['Keys'][0]['Volume']==40 and host.config['Banks'][0]['Keys'][1]['Volume']==12,'shared action keeps independent key volumes')
        host.command('action',index=0,shared=True,action={'Name':'Nome compartilhado','Kind':'audio','AssetId':asset['Id']})
        check(next(a for a in host.config['Actions'] if a['Id']==action)['Name']=='Nome compartilhado','shared edit updates original action')
        check('16 posições' in host.rejected('action',index=16,action={'Name':'Extra','Kind':'stop'}),'inactive key cannot be configured directly')
        host.command('freeKey',index=16,replace=15)
        host.command('action',index=16,action={'Name':'Extra','Kind':'stop'})
        check(host.config['Banks'][0]['Keys'][16]['ActionId']!='' and len(host.config['FreeKeys'])==16,'free can move a selected position')
        mock=FakeObs()
        host.command('obs',port=mock.port,password='test-password',remember=True)
        check('test-password' not in (root/'settings.json').read_text(),'OBS password absent in plaintext settings')
        host.command('action',index=2,action={'Name':'Transmitir','Kind':'streamStart'})
        host.command('press',index=2,token=-3);time.sleep(.15);host.command('release',token=-3);time.sleep(1.1)
        check('StartStream' not in mock.requests,'short press does not start stream')
        host.command('press',index=2,token=-3);time.sleep(1.2);host.command('release',token=-3)
        check(mock.requests.count('StartStream')==1,'long press executes once against fake OBS')
        host.command('press',index=2,token=-3);host.command('stop');time.sleep(1.2);host.command('release',token=-3)
        check(mock.requests.count('StartStream')==1,'stop all cancels confirmation')
        host.command('action',index=3,action={'Name':'Cena','Kind':'scene','Target':'Pausa'})
        host.command('press',index=3,token=-4);time.sleep(.2);host.command('release',token=-4)
        check('SetCurrentProgramScene' in mock.requests,'scene control via OBS protocol')
        host.command('obsOff')
        check(not mock.errors,'fake OBS completed without errors')
        host.close();host=Host(root)
        check(host.config['ActiveBank']==bank and host.config['Banks'][0]['Name']=='Conversa','restart restores bank and active selection')
        check(host.config['Banks'][0]['Keys'][1]['Volume']==12 and 16 in host.config['FreeKeys'],'key volume and selected free position survive restart')
        host.command('hello');state=host.until(lambda m:m['type']=='status')['data']
        check(not state['useMode'] and state['playing']==0,'restart does not execute actions or resume sounds')
        host.process.stdin.close()
        check(host.process.wait(5)==0,'closing UI pipe shuts down engine without orphan process')
        print('Protocol tests passed. Data:',root,flush=True)
    finally:host.close()

if __name__=='__main__':main()
